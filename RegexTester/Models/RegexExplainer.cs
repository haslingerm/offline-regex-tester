using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;

namespace RegexTester.Models;

/// <summary>
///     Explains a regex pattern by compiling a tiny scratch library project that has
///     [GeneratedRegex] on a method. The GeneratedRegex source generator emits an
///     XML-doc &lt;remarks&gt; block with a human-readable explanation of each pattern
///     component. We capture that output and display it.
///     The scratch directory lives in the OS temp folder so the OS handles cleanup.
///     A semaphore ensures only one build runs at a time (they're not reentrant).
/// </summary>
public static class RegexExplainer
{
    private static readonly string scratchDir =
        Path.Combine(Path.GetTempPath(), "RegexTesterScratch");

    private static readonly SemaphoreSlim @lock = new(1, 1);

    public static async Task<ExplanationResult> ExplainAsync(
        string pattern,
        RegexOptions options,
        CancellationToken ct = default)
    {
        // Let OperationCanceledException propagate so callers can detect cancellation.
        await @lock.WaitAsync(ct);
        try
        {
            try
            {
                EnsureScratchProject();
                WriteSourceFile(pattern, options);

                var (exitCode, buildOutput) = await RunBuildAsync(ct);
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ct);
                }

                if (exitCode != 0)
                {
                    return ExplanationResult.BuildFailed(buildOutput);
                }

                var lines = ParseGeneratedFile();

                return lines.Count > 0
                    ? ExplanationResult.Ok(lines)
                    : ExplanationResult.NotFound();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ExplanationResult.Unexpected(ex);
            }
        }
        finally
        {
            @lock.Release();
        }
    }

    private static void EnsureScratchProject()
    {
        Directory.CreateDirectory(scratchDir);
        var csproj = Path.Combine(scratchDir, "Scratch.csproj");
        if (File.Exists(csproj))
        {
            return;
        }

        File.WriteAllText(csproj, """
                                  <Project Sdk="Microsoft.NET.Sdk">
                                    <PropertyGroup>
                                      <TargetFramework>net10.0</TargetFramework>
                                      <Nullable>enable</Nullable>
                                      <ImplicitUsings>enable</ImplicitUsings>
                                      <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                                    </PropertyGroup>
                                  </Project>
                                  """);
    }

    private static void WriteSourceFile(string pattern, RegexOptions options)
    {
        // Escape for verbatim C# string: double any embedded double-quotes.
        var escaped = pattern.Replace("\"", "\"\"");

        var optParts = new List<string>();
        if (options.HasFlag(RegexOptions.IgnoreCase))
        {
            optParts.Add("RegexOptions.IgnoreCase");
        }

        if (options.HasFlag(RegexOptions.Multiline))
        {
            optParts.Add("RegexOptions.Multiline");
        }

        if (options.HasFlag(RegexOptions.Singleline))
        {
            optParts.Add("RegexOptions.Singleline");
        }

        if (options.HasFlag(RegexOptions.IgnorePatternWhitespace))
        {
            optParts.Add("RegexOptions.IgnorePatternWhitespace");
        }

        if (options.HasFlag(RegexOptions.ExplicitCapture))
        {
            optParts.Add("RegexOptions.ExplicitCapture");
        }

        var optArg = optParts.Count > 0
            ? $", {string.Join(" | ", optParts)}"
            : "";

        var source = $$"""
                       using System.Text.RegularExpressions;

                       public partial class ScratchRegex
                       {
                           [GeneratedRegex(@"{{escaped}}"{{optArg}})]
                           public static partial Regex GetPattern();
                       }
                       """;

        File.WriteAllText(Path.Combine(scratchDir, "ScratchRegex.cs"), source);
    }

    private static async Task<(int exitCode, string output)> RunBuildAsync(CancellationToken ct)
    {
        var result = await Cli.Wrap("dotnet")
            .WithArguments(["build", "--nologo", "-v", "quiet"])
            .WithWorkingDirectory(scratchDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);

        var combined = (result.StandardOutput + "\n" + result.StandardError).Trim();

        return (result.ExitCode, combined);
    }

    /// <summary>
    ///     The RegexGenerator emits a file at a known path under obj/.
    ///     We read it and extract the Explanation block from the XML doc comments.
    ///     The generated remarks look like:
    ///     <code>
    ///   /// Explanation:<br />
    ///   /// <code>
    ///   /// ○ 1st capture group.<br />
    ///   ///     ○ Match a word character greedily at least once.<br />
    ///   /// ○ Match a whitespace character any number of times.<br />
    ///   /// </code>
    /// </code>
    /// </summary>
    private static List<ExplanationLine> ParseGeneratedFile()
    {
        // The generator writes to this fixed path (relative to ScratchDir):
        //   obj/Debug/net10.0/generated/
        //     System.Text.RegularExpressions.Generator/
        //       System.Text.RegularExpressions.Generator.RegexGenerator/
        //         RegexGenerator.g.cs
        var generatedDir = Path.Combine(scratchDir, "obj", "Debug", "net10.0", "generated",
            "System.Text.RegularExpressions.Generator",
            "System.Text.RegularExpressions.Generator.RegexGenerator");

        var generatedFile = Path.Combine(generatedDir, "RegexGenerator.g.cs");

        if (!File.Exists(generatedFile))
        {
            // Fallback: glob for any .g.cs that isn't the global usings file
            var found = Directory.GetFiles(Path.Combine(scratchDir, "obj"),
                "*.g.cs",
                SearchOption.AllDirectories);

            generatedFile = Array.Find(found,
                                f => !f.EndsWith("GlobalUsings.g.cs", StringComparison.OrdinalIgnoreCase))
                            ?? string.Empty;
        }

        if (!File.Exists(generatedFile))
        {
            return [];
        }

        return ExtractExplanation(File.ReadAllLines(generatedFile));
    }

    private static List<ExplanationLine> ExtractExplanation(string[] lines)
    {
        var result = new List<ExplanationLine>();
        var inCode = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].TrimStart();

            // Only look inside XML doc comment lines
            if (!raw.StartsWith("///"))
            {
                continue;
            }

            // Strip the "/// " prefix (keep trailing content)
            var content = raw.Length > 4 && raw[3] == ' '
                ? raw[4..]
                : raw[3..];

            if (!inCode)
            {
                // Look for the start of the explanation <code> block that follows "Explanation:"
                if (content.TrimEnd() == "<code>"
                    && i > 0
                    && lines[i - 1].TrimStart().TrimStart('/').Trim()
                        .StartsWith("Explanation:", StringComparison.OrdinalIgnoreCase))
                    inCode = true;

                continue;
            }

            // End of explanation block
            if (content.TrimEnd() == "</code>")
            {
                break;
            }

            // Strip trailing <br/> and whitespace
            var text = content
                .Replace("<br/>", "")
                .Replace("<br />", "")
                .TrimEnd();

            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            // Count leading spaces before the ○ bullet to determine indent level.
            // Generator uses 4-space indentation per level.
            var leadingSpaces = text.Length - text.TrimStart().Length;
            var indent = leadingSpaces / 4;

            result.Add(new ExplanationLine(text.TrimStart(), indent));
        }

        return result;
    }
}

public sealed class ExplanationResult
{
    public bool IsOk { get; private init; }
    public IReadOnlyList<ExplanationLine> Lines { get; private init; } = [];
    public string? ErrorMessage { get; private init; }

    public static ExplanationResult Ok(List<ExplanationLine> lines)
    {
        return new ExplanationResult { IsOk = true, Lines = lines };
    }

    public static ExplanationResult BuildFailed(string output)
    {
        return new ExplanationResult { IsOk = false, ErrorMessage = $"Build failed (invalid pattern?)\n{output}" };
    }

    public static ExplanationResult NotFound()
    {
        return new ExplanationResult
            { IsOk = false, ErrorMessage = "Source generator produced no explanation for this pattern." };
    }

    public static ExplanationResult Unexpected(Exception ex)
    {
        return new ExplanationResult { IsOk = false, ErrorMessage = $"Explanation unavailable: {ex.Message}" };
    }
}

public sealed record ExplanationLine(string Text, int IndentLevel);