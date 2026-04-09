using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RegexTester.Converters;

/// <summary>Static instances of simple converters, referenced from XAML via x:Static.</summary>
public static class ValueConverters
{
    private static readonly IBrush matchGreen = new SolidColorBrush(Color.Parse("#50fa7b"));
    private static readonly IBrush dimGray = new SolidColorBrush(Color.Parse("#44475a"));
    private static readonly IBrush matchBg = new SolidColorBrush(Color.Parse("#162b1c"));
    private static readonly IBrush noMatchBg = new SolidColorBrush(Color.Parse("#1e1f2e"));

    /// <summary>bool HasMatch → green/gray foreground brush for the indicator dot.</summary>
    public static readonly IValueConverter MatchDotBrush =
        new FuncValueConverter<bool, IBrush?>(v => v ? matchGreen : dimGray);

    /// <summary>bool HasMatch → slightly tinted background for match rows.</summary>
    public static readonly IValueConverter MatchRowBackground =
        new FuncValueConverter<bool, IBrush?>(v => v ? matchBg : noMatchBg);

    /// <summary>int IndentLevel → Thickness left-margin (20 px per level).</summary>
    public static readonly IValueConverter Indent =
        new FuncValueConverter<int, Thickness>(level => new Thickness(level * 20, 0, 0, 0));

    /// <summary>bool HasMatch → "●" or "○".</summary>
    public static readonly IValueConverter MatchDotText =
        new FuncValueConverter<bool, string?>(v => v ? "●" : "○");
}