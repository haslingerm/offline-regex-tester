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

    // Cached async detection: runs once, result is reused for the lifetime of the process.
    private static readonly Lazy<Task<string?>> _tfmDetection =
        new(() => DetectTfmAsync(CancellationToken.None));

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
                string? tfm = await _tfmDetection.Value;
                if (tfm == null)
                {
                    return ExplanationResult.RuntimeTooOld();
                }

                EnsureScratchProject(tfm);
                WriteSourceFile(pattern, options);

                (int exitCode, string buildOutput) = await RunBuildAsync(ct);
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ct);
                }

                if (exitCode != 0)
                {
                    return ExplanationResult.BuildFailed(buildOutput);
                }

                List<ExplanationLine> lines = ParseGeneratedFile(tfm);

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

    private static async Task<string?> DetectTfmAsync(CancellationToken ct)
    {
        try
        {
            var result = await Cli.Wrap("dotnet")
                                  .WithArguments(["--list-sdks"])
                                  .WithValidation(CommandResultValidation.None)
                                  .ExecuteBufferedAsync(ct);

            if (result.ExitCode != 0)
            {
                return null;
            }

            // Each line looks like: "9.0.100 [/path/to/sdk]"
            var highestMajor = 0;
            foreach (string line in result.StandardOutput.Split('\n'))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                string versionStr = trimmed.Split(' ', 2)[0];
                if (Version.TryParse(versionStr, out var version) && version.Major >= 9)
                {
                    highestMajor = Math.Max(highestMajor, version.Major);
                }
            }

            return highestMajor >= 9 ? $"net{highestMajor}.0" : null;
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureScratchProject(string tfm)
    {
        Directory.CreateDirectory(scratchDir);
        string csproj = Path.Combine(scratchDir, "Scratch.csproj");

        // Always write (or overwrite) the csproj so that a stale file from a previous
        // installation with a different TFM is corrected on the next run.
        // Since the TFM is detected once per process lifetime the file content is
        // stable during a single session; redundant writes are cheap for a tiny file.
        File.WriteAllText(csproj, $"""
                                   <Project Sdk="Microsoft.NET.Sdk">
                                     <PropertyGroup>
                                       <TargetFramework>{tfm}</TargetFramework>
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
        string escaped = pattern.Replace("\"", "\"\"");

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

        string optArg = optParts.Count > 0
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

        string combined = (result.StandardOutput + "\n" + result.StandardError).Trim();

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
    private static List<ExplanationLine> ParseGeneratedFile(string tfm)
    {
        // The generator writes to this fixed path (relative to ScratchDir):
        //   obj/Debug/{tfm}/generated/
        //     System.Text.RegularExpressions.Generator/
        //       System.Text.RegularExpressions.Generator.RegexGenerator/
        //         RegexGenerator.g.cs
        string generatedDir = Path.Combine(scratchDir, "obj", "Debug", tfm, "generated",
                                           "System.Text.RegularExpressions.Generator",
                                           "System.Text.RegularExpressions.Generator.RegexGenerator");

        string generatedFile = Path.Combine(generatedDir, "RegexGenerator.g.cs");

        if (!File.Exists(generatedFile))
        {
            // Fallback: glob for any .g.cs that isn't the global usings file
            string[] found = Directory.GetFiles(Path.Combine(scratchDir, "obj"),
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
            string raw = lines[i].TrimStart();

            // Only look inside XML doc comment lines
            if (!raw.StartsWith("///"))
            {
                continue;
            }

            // Strip the "/// " prefix (keep trailing content)
            string content = raw.Length > 4 && raw[3] == ' '
                ? raw[4..]
                : raw[3..];

            if (!inCode)
            {
                // Look for the start of the explanation <code> block that follows "Explanation:"
                if (content.TrimEnd() == "<code>"
                    && i > 0
                    && lines[i - 1].TrimStart().TrimStart('/').Trim()
                                   .StartsWith("Explanation:", StringComparison.OrdinalIgnoreCase))
                {
                    inCode = true;
                }

                continue;
            }

            // End of explanation block
            if (content.TrimEnd() == "</code>")
            {
                break;
            }

            // Strip trailing <br/> and whitespace
            string text = content
                          .Replace("<br/>", "")
                          .Replace("<br />", "")
                          .TrimEnd();

            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            // Count leading spaces before the ○ bullet to determine indent level.
            // Generator uses 4-space indentation per level.
            int leadingSpaces = text.Length - text.TrimStart().Length;
            int indent = leadingSpaces / 4;

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

    public static ExplanationResult Ok(List<ExplanationLine> lines) => new() { IsOk = true, Lines = lines };

    public static ExplanationResult RuntimeTooOld() =>
        new() { IsOk = false, ErrorMessage = "This feature requires the .NET 9 SDK (or later) to be installed on this system." };

    public static ExplanationResult BuildFailed(string output) =>
        new() { IsOk = false, ErrorMessage = $"Build failed (invalid pattern?)\n{output}" };

    public static ExplanationResult NotFound() =>
        new() { IsOk = false, ErrorMessage = "Source generator produced no explanation for this pattern." };

    public static ExplanationResult Unexpected(Exception ex) =>
        new() { IsOk = false, ErrorMessage = $"Explanation unavailable: {ex.Message}" };
}

public sealed record ExplanationLine(string Text, int IndentLevel);
