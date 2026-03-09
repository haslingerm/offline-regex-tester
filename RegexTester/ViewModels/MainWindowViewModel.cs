using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RegexTester.Models;

namespace RegexTester.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private CancellationTokenSource? _explainCts;

    [ObservableProperty]
    private string? _explanationError;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _hasExplanation;

    [ObservableProperty]
    private bool _ignoreCase;

    [ObservableProperty]
    private bool _ignorePatternWhitespace;

    [ObservableProperty]
    private bool _isExplaining;

    [ObservableProperty]
    private string _matchSummary = "";

    [ObservableProperty]
    private bool _multiline;

    [ObservableProperty]
    private string _pattern = @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})";

    [ObservableProperty]
    private string? _regexError;

    [ObservableProperty]
    private bool _singleline;

    [ObservableProperty]
    private string _testString =
        "2024-03-09\nhello world\n1999-12-31\nnot-a-date\n2026-01-15\n12-3-456 (wrong format)";

    public MainWindowViewModel()
    {
        UpdateMatches();
        ScheduleExplain();
    }

    public ObservableCollection<LineResult> LineResults { get; } = [];
    public ObservableCollection<ExplanationLine> ExplanationLines { get; } = [];

    partial void OnPatternChanged(string value)
    {
        UpdateMatches();
        ScheduleExplain();
    }

    partial void OnTestStringChanged(string value) => UpdateMatches();

    partial void OnIgnoreCaseChanged(bool value)
    {
        UpdateMatches();
        ScheduleExplain();
    }

    partial void OnMultilineChanged(bool value)
    {
        UpdateMatches();
        ScheduleExplain();
    }

    partial void OnSinglelineChanged(bool value)
    {
        UpdateMatches();
        ScheduleExplain();
    }

    partial void OnIgnorePatternWhitespaceChanged(bool value)
    {
        UpdateMatches();
        ScheduleExplain();
    }

    private void UpdateMatches()
    {
        LineResults.Clear();
        RegexError = null;
        HasError = false;
        MatchSummary = "";

        if (string.IsNullOrEmpty(Pattern))
        {
            return;
        }

        Regex regex;
        try
        {
            regex = new Regex(Pattern, BuildOptions(), TimeSpan.FromSeconds(2));
        }
        catch (ArgumentException ex)
        {
            RegexError = ex.Message;
            HasError = true;

            return;
        }

        var groupNames = regex.GetGroupNames();
        var rawLines = (TestString ?? "").Split('\n');

        var totalMatches = 0;
        var matchedLines = 0;

        for (var i = 0; i < rawLines.Length; i++)
        {
            var line = rawLines[i].TrimEnd('\r');
            MatchCollection matches;
            try
            {
                matches = regex.Matches(line);
            }
            catch (RegexMatchTimeoutException)
            {
                break;
            }

            var matchList = new List<MatchResult>();
            foreach (Match m in matches)
            {
                var groups = new List<GroupResult>();
                for (var g = 1; g < m.Groups.Count; g++)
                {
                    var grp = m.Groups[g];
                    groups.Add(new GroupResult
                    {
                        Name = groupNames[g],
                        Value = grp.Value,
                        Success = grp.Success
                    });
                }

                matchList.Add(new MatchResult { Value = m.Value, Index = m.Index, Groups = groups });
                totalMatches++;
            }

            if (matchList.Count > 0)
            {
                matchedLines++;
            }

            LineResults.Add(new LineResult
            {
                LineNumber = i + 1,
                LineText = line,
                Matches = matchList
            });
        }

        var lines = rawLines.Length;
        MatchSummary = totalMatches switch
                       {
                           0 => "No matches found",
                           1 => $"1 match in {matchedLines}/{lines} line{(lines == 1 ? "" : "s")}",
                           _ => $"{totalMatches} matches in {matchedLines}/{lines} line{(lines == 1 ? "" : "s")}"
                       };
    }

    private void ScheduleExplain()
    {
        _explainCts?.Cancel();
        _explainCts = new CancellationTokenSource();
        var token = _explainCts.Token;

        ExplanationLines.Clear();
        HasExplanation = false;
        ExplanationError = null;

        if (string.IsNullOrEmpty(Pattern) || HasError)
        {
            return;
        }

        _ = RunExplainAfterDelayAsync(token);
    }

    private async Task RunExplainAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(800, ct);
            await RunExplainAsync(ct);
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private async Task ExplainNowAsync()
    {
        _explainCts?.Cancel();
        _explainCts = new CancellationTokenSource();
        ExplanationLines.Clear();
        HasExplanation = false;
        ExplanationError = null;

        if (string.IsNullOrEmpty(Pattern) || HasError)
        {
            return;
        }

        await RunExplainAsync(_explainCts.Token);
    }

    private async Task RunExplainAsync(CancellationToken ct)
    {
        IsExplaining = true;
        try
        {
            var result = await RegexExplainer.ExplainAsync(Pattern, BuildOptions(), ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            ExplanationLines.Clear();
            if (result.IsOk)
            {
                foreach (var line in result.Lines)
                {
                    ExplanationLines.Add(line);
                }

                HasExplanation = result.Lines.Count > 0;
                ExplanationError = HasExplanation ? null : "No explanation available for this pattern.";
            }
            else
            {
                HasExplanation = false;
                ExplanationError = result.ErrorMessage;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                ExplanationError = $"Error: {ex.Message}";
            }
        }
        finally
        {
            IsExplaining = false;
        }
    }

    private RegexOptions BuildOptions()
    {
        var opts = RegexOptions.None;
        if (IgnoreCase)
        {
            opts |= RegexOptions.IgnoreCase;
        }

        if (Multiline)
        {
            opts |= RegexOptions.Multiline;
        }

        if (Singleline)
        {
            opts |= RegexOptions.Singleline;
        }

        if (IgnorePatternWhitespace)
        {
            opts |= RegexOptions.IgnorePatternWhitespace;
        }

        return opts;
    }
}
