// Behaviors/FavoritoBehavior.cs
using CafeteriaInsti.Models;
using CafeteriaInsti.Services;
using Microsoft.Maui.Controls;

namespace CafeteriaInsti.Behaviors
{
    public class FavoritoBehavior : Behavior<Label>
    {
        private Label? _label;
        private FavoritosService? _favoritosService;

        protected override void OnAttachedTo(Label bindable)
        {
            base.OnAttachedTo(bindable);
            _label = bindable;
            
            // Obtener el servicio de favoritos del service provider
            _favoritosService = Application.Current?.Handler?.MauiContext?.Services.GetService<FavoritosService>();
            
            if (_favoritosService != null)
            {
                _favoritosService.FavoritoToggled += OnFavoritoToggled;
            }
            
            _label.BindingContextChanged += OnBindingContextChanged;
        }

        protected override void OnDetachingFrom(Label bindable)
        {
            base.OnDetachingFrom(bindable);
            
            if (_favoritosService != null)
            {
                _favoritosService.FavoritoToggled -= OnFavoritoToggled;
            }
            
            if (_label != null)
            {
                _label.BindingContextChanged -= OnBindingContextChanged;
            }
        }

        private void OnBindingContextChanged(object? sender, EventArgs e)
        {
            ActualizarEstadoFavorito();
        }

        private void OnFavoritoToggled(object? sender, int productoId)
        {
            if (_label?.BindingContext is Producto producto && producto.Id == productoId)
            {
                ActualizarEstadoFavorito();
            }
        }

        private void ActualizarEstadoFavorito()
        {
            if (_label?.BindingContext is Producto producto && _favoritosService != null)
            {
                var esFavorito = _favoritosService.IsFavorito(producto.Id);
                _label.TextColor = esFavorito ? Color.FromArgb("#E91E63") : Color.FromArgb("#CCCCCC");
                // Usar un símbolo más compatible
                _label.Text = esFavorito ? "?" : "?";
                _label.FontFamily = "OpenSansRegular"; // Usar fuente del sistema
            }
        }
    }
}
