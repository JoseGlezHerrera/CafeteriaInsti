// Converters/BoolToHeartConverter.cs
using System.Globalization;

namespace CafeteriaInsti.Converters
{
    public class BoolToHeartConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isFavorite)
            {
                return isFavorite ? "??" : "??";
            }
            return "??";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
