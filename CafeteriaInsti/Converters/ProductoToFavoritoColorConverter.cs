// Converters/ProductoToFavoritoColorConverter.cs
using System.Globalization;
using CafeteriaInsti.Models;

namespace CafeteriaInsti.Converters
{
    public class ProductoToFavoritoColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Producto producto && parameter is Services.FavoritosService service)
            {
                return service.IsFavorito(producto.Id) ? Color.FromArgb("#E91E63") : Color.FromArgb("#CCCCCC");
            }
            return Color.FromArgb("#CCCCCC");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
