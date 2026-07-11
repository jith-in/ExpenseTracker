using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ExpenseTracker.Converters
{
    public class StringNotNullOrEmptyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is string text && !string.IsNullOrWhiteSpace(text);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
