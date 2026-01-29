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
                return category switch
                {
                    "Bebidas Calientes" => "?",
                    "Bebidas Frías" => "??",
                    "Postres" => "??",
                    "Snacks" => "??",
                    _ => "??"
                };
            }
            return "??";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
