namespace RegexTester.Models;

public class GroupResult
{
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
    public bool Success { get; init; }
}

public class MatchResult
{
    public string Value { get; init; } = "";
    public int Index { get; init; }
    public IReadOnlyList<GroupResult> Groups { get; init; } = [];
}

public class LineResult
{
    public int LineNumber { get; init; }
    public string LineText { get; init; } = "";
    public bool HasMatch => Matches.Count > 0;
    public IReadOnlyList<MatchResult> Matches { get; init; } = [];
}
