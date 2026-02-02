// ViewModels/FavoritosViewModel.cs
using CafeteriaInsti.Models;
using CafeteriaInsti.Services;
using CafeteriaInsti.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CafeteriaInsti.ViewModels
{
    public partial class FavoritosViewModel : BaseViewModel
    {
        private readonly FavoritosService _favoritosService;
        private readonly ProductoService _productoService;

        [ObservableProperty]
        private ObservableCollection<Producto> _productosFavoritos;

        [ObservableProperty]
        private bool _hayFavoritos;

        public FavoritosViewModel(FavoritosService favoritosService, ProductoService productoService)
        {
            Title = "Mis Favoritos";
            _favoritosService = favoritosService;
            _productoService = productoService;
            _productosFavoritos = new ObservableCollection<Producto>();
            
            // Suscribirse a cambios en favoritos
            _favoritosService.FavoritoToggled += OnFavoritoToggled;
            
            CargarFavoritos();
        }

        private void OnFavoritoToggled(object? sender, int productoId)
        {
            CargarFavoritos();
        }

        public void CargarFavoritos()
        {
            var favoritosIds = _favoritosService.GetFavoritosIds();
            var todosProductos = _productoService.GetProductos();
            
            ProductosFavoritos.Clear();
            
            foreach (var id in favoritosIds)
            {
                var producto = todosProductos.FirstOrDefault(p => p.Id == id);
                if (producto != null)
                {
                    ProductosFavoritos.Add(producto);
                }
            }
            
            HayFavoritos = ProductosFavoritos.Any();
            System.Diagnostics.Debug.WriteLine($"[INFO] Favoritos cargados: {ProductosFavoritos.Count}");
        }

        [RelayCommand]
        private async Task GoToDetailsAsync(Producto producto)
        {
            if (producto == null) return;

            var navigationParameter = new Dictionary<string, object>
            {
                { "Producto", producto }
            };

            await Shell.Current.GoToAsync(nameof(DetalleProductoPage), navigationParameter);
        }

        [RelayCommand]
        private void QuitarDeFavoritos(Producto producto)
        {
            if (producto == null) return;
            
            _favoritosService.ToggleFavorito(producto.Id);
            // OnFavoritoToggled se llamará automáticamente
        }

        [RelayCommand]
        private async Task LimpiarFavoritos()
        {
            var confirmar = await Shell.Current.DisplayAlert(
                "Limpiar Favoritos",
                "¿Estás seguro de que deseas eliminar todos los favoritos?",
                "Sí, eliminar",
                "Cancelar");

            if (confirmar)
            {
                _favoritosService.LimpiarFavoritos();
                CargarFavoritos();
                await Shell.Current.DisplayAlert("Favoritos", "Favoritos eliminados correctamente", "OK");
            }
        }
    }
}
