// Converters/CategoryTextConverter.cs
using System.Globalization;

namespace CafeteriaInsti.Converters
{
    public class CategoryTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string category)
            {
                // ? Devolver las primeras letras de cada categoria en lugar de emojis
                return category switch
                {
                    "Bebidas Calientes" => "HOT",
                    "Bebidas Frias" => "COLD",
                    "Postres" => "POST",
                    "Snacks" => "SNK",
                    _ => "---"
                };
            }
            return "---";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

