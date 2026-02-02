// Converters/CategoryBackgroundConverter.cs
using System.Globalization;

namespace CafeteriaInsti.Converters
{
    public class CategoryBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string category)
            {
                return category switch
                {
                    "Bebidas Calientes" => Color.FromArgb("#FFF3E0"),
                    "Bebidas Frias" => Color.FromArgb("#E3F2FD"),
                    "Postres" => Color.FromArgb("#FCE4EC"),
                    "Snacks" => Color.FromArgb("#F1F8E9"),
                    _ => Color.FromArgb("#F5F5F5")
                };
            }
            return Color.FromArgb("#F5F5F5");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
