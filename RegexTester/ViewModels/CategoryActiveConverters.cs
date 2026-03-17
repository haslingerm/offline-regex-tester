using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace RegexTester.ViewModels
{
    public class CategoryActiveBackgroundConverter : Avalonia.Data.Converters.IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value is bool active && active) ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#383a4a")) : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#23243a"));
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class CategoryActiveForegroundConverter : Avalonia.Data.Converters.IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value is bool active && active) ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#bd93f9")) : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#f8f8f2"));
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
