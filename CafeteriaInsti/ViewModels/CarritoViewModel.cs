// ViewModels/CarritoViewModel.cs
using CafeteriaInsti.Models;
using CafeteriaInsti.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CafeteriaInsti.ViewModels
{
    public partial class CarritoViewModel : BaseViewModel
    {
        private readonly CarritoService _carritoService;

        [ObservableProperty]
        private ObservableCollection<ItemCarrito> _items;

        [ObservableProperty]
        private decimal _total;

        public CarritoViewModel(CarritoService carritoService)
        {
            Title = "Mi Carrito";
            _carritoService = carritoService;
            _items = _carritoService.Items;
            CalcularTotal();
        }

        [RelayCommand]
        private void EliminarItem(int productoId)
        {
            _carritoService.EliminarDelCarrito(productoId);
            CalcularTotal();
        }

        [RelayCommand]
        private void ActualizarCantidad(ItemCarrito item)
        {
            _carritoService.ActualizarCantidad(item.Producto.Id, item.Cantidad);
            CalcularTotal();
        }

        [RelayCommand]
        private async Task FinalizarPedidoAsync()
        {
            if (!Items.Any())
            {
                await Shell.Current.DisplayAlert("Carrito Vacío", "Tu carrito está vacío. Añade algo antes de finalizar el pedido.", "OK");
                return;
            }

            // Aquí iría la lógica para procesar el pedido
            await Shell.Current.DisplayAlert("Pedido Realizado", $"Tu pedido de {Total:C} ha sido realizado con éxito.", "OK");

            // Limpiamos el carrito
            _carritoService.LimpiarCarrito();

            // Volvemos al menú principal
            await Shell.Current.GoToAsync("//ListaProductosPage");
        }

        private void CalcularTotal()
        {
            Total = _carritoService.GetTotal();
        }

        [RelayCommand]
        private void IncrementarCantidad(ItemCarrito item)
        {
            item.Cantidad++;
            _carritoService.ActualizarCantidad(item.Producto.Id, item.Cantidad);
            CalcularTotal();
        }

        [RelayCommand]
        private void DecrementarCantidad(ItemCarrito item)
        {
            if (item.Cantidad > 1)
            {
                item.Cantidad--;
                _carritoService.ActualizarCantidad(item.Producto.Id, item.Cantidad);
                CalcularTotal();
            }
        }


    }
}