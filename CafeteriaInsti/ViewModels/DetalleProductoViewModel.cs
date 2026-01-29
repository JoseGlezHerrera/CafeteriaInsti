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
        private Producto _producto = new Producto();

        [ObservableProperty]
        private int _cantidad = 1;

        public DetalleProductoViewModel(CarritoService carritoService)
        {
            _carritoService = carritoService;
        }

        // ✅ CORREGIDO: Ahora es async Task con await
        [RelayCommand]
        private async Task AnadirAlCarritoAsync()
        {
            if (Cantidad > 0)
            {
                for (int i = 0; i < Cantidad; i++)
                {
                    _carritoService.AnadirAlCarrito(Producto);
                }

                // ✅ Usar await correctamente
                await Shell.Current.DisplayAlertAsync("Añadido", $"{Cantidad} x {Producto.Nombre} añadido al carrito", "OK");

                // ✅ Usar await correctamente
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