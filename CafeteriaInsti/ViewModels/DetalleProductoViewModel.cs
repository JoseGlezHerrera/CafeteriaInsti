// ViewModels/DetalleProductoViewModel.cs
using CafeteriaInsti.Models;
using CafeteriaInsti.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeteriaInsti.ViewModels
{
    [QueryProperty("Producto", "Producto")]
    public partial class DetalleProductoViewModel : BaseViewModel
    {
        private readonly CarritoService _carritoService;

        [ObservableProperty]
        private Producto _producto;

        [ObservableProperty]
        private int _cantidad = 1;

        public DetalleProductoViewModel(CarritoService carritoService)
        {
            _carritoService = carritoService;
        }

        [RelayCommand]
        private async Task AnadirAlCarritoAsync() // ✅ Cambiar a async Task
        {
            if (Cantidad > 0)
            {
                for (int i = 0; i < Cantidad; i++)
                {
                    _carritoService.AnadirAlCarrito(Producto);
                }

                // ✅ Usar await
                await Shell.Current.DisplayAlert("Añadido", $"{Cantidad} x {Producto.Nombre} añadido al carrito", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        [RelayCommand]
        private void IncrementarCantidad()
        {
            Cantidad++;
        }

        [RelayCommand]
        private void DecrementarCantidad()
        {
            if (Cantidad > 1)
                Cantidad--;
        }
    }
}