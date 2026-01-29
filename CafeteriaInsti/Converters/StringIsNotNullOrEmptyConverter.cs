// Converters/StringIsNotNullOrEmptyConverter.cs
using System.Globalization;

namespace CafeteriaInsti.Converters
{
    public class StringIsNotNullOrEmptyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return !string.IsNullOrEmpty(text);
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
