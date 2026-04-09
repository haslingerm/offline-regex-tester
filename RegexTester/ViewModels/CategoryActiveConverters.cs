using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RegexTester.ViewModels;

// Shared brushes for the category active/inactive states so they're instantiated once.
internal static class CategoryActiveBrushes
{
    public static readonly IBrush ActiveBackground = new SolidColorBrush(Color.Parse("#383a4a"));
    public static readonly IBrush InactiveBackground = new SolidColorBrush(Color.Parse("#23243a"));
    public static readonly IBrush ActiveForeground = new SolidColorBrush(Color.Parse("#bd93f9"));
    public static readonly IBrush InactiveForeground = new SolidColorBrush(Color.Parse("#f8f8f2"));
}

public class CategoryActiveBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool and true
            ? CategoryActiveBrushes.ActiveBackground
            : CategoryActiveBrushes.InactiveBackground;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class CategoryActiveForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool and true
            ? CategoryActiveBrushes.ActiveForeground
            : CategoryActiveBrushes.InactiveForeground;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
