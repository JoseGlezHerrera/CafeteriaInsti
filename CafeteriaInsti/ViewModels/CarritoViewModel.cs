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

            // ✅ CORREGIDO: Suscribirse a cambios en la colección
            _items.CollectionChanged += (s, e) => CalcularTotal();
        }

        [RelayCommand]
        private void EliminarItem(int productoId)
        {
            try
            {
                _carritoService.EliminarDelCarrito(productoId);
                
#if ANDROID || IOS
                HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
#endif
                
                CalcularTotal();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Eliminar item: {ex.Message}");
            }
        }

        [RelayCommand]
        private void IncrementarCantidad(ItemCarrito item)
        {
            try
            {
                item.Cantidad++;
                _carritoService.ActualizarCantidad(item.Producto.Id, item.Cantidad);
                
#if ANDROID || IOS
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
#endif
                
                CalcularTotal();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Incrementar cantidad: {ex.Message}");
            }
        }

        [RelayCommand]
        private void DecrementarCantidad(ItemCarrito item)
        {
            try
            {
                if (item.Cantidad > 1)
                {
                    item.Cantidad--;
                    _carritoService.ActualizarCantidad(item.Producto.Id, item.Cantidad);
                    
#if ANDROID || IOS
                    HapticFeedback.Default.Perform(HapticFeedbackType.Click);
#endif
                    
                    CalcularTotal();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Decrementar cantidad: {ex.Message}");
            }
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
            try
            {
                if (!Items.Any())
                {
                    await Shell.Current.DisplayAlertAsync("Carrito Vacío", "Tu carrito está vacío. Añade algo antes de finalizar el pedido.", "OK");
                    return;
                }

#if ANDROID || IOS
                HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
#endif

                // Generar número de pedido único
                var numeroPedido = $"CF{DateTime.Now:yyyyMMddHHmmss}";
                var cantidadItems = Items.Sum(i => i.Cantidad);
                var totalPedido = Total;

                // Limpiamos el carrito
                _carritoService.LimpiarCarrito();

                // Navegamos a la página de confirmación con los parámetros
                var navigationParameter = new Dictionary<string, object>
                {
                    { "NumeroPedido", numeroPedido },
                    { "TotalString", totalPedido.ToString() },
                    { "CantidadItemsString", cantidadItems.ToString() }
                };

                await Shell.Current.GoToAsync("ConfirmacionPedidoPage", navigationParameter);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Finalizar pedido: {ex.Message}");
                await Shell.Current.DisplayAlertAsync("Error", "Hubo un problema al procesar tu pedido. Inténtalo de nuevo.", "OK");
            }
        }

        private void CalcularTotal()
        {
            Total = _carritoService.GetTotal();
        }
    }
}