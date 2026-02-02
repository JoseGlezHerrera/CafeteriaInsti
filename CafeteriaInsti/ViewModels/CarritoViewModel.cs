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
            
            // ✅ CORREGIDO: Usar directamente la colección del servicio (no crear copia)
            _items = _carritoService.Items;
            
            CalcularTotal();

            // ✅ Suscribirse a cambios en la colección para recalcular el total automáticamente
            _carritoService.Items.CollectionChanged += (s, e) => 
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] CarritoService.Items cambió - Count: {_carritoService.Items.Count}");
                CalcularTotal();
            };
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

                // Calcular datos del pedido
                var cantidadItems = Items.Sum(i => i.Cantidad);
                var totalPedido = Total;
                
                // Pedir confirmación antes de finalizar el pedido
                var confirmar = await Shell.Current.DisplayAlert(
                    "Confirmar Pedido",
                    $"Confirmas tu pedido?\n\n" +
                    $"• {cantidadItems} articulo(s)\n" +
                    $"• Total: {totalPedido:C}\n\n" +
                    $"Deseas continuar?",
                    "Si, Confirmar",
                    "No, Revisar");

                if (!confirmar)
                {
                    // Usuario canceló, se queda en el carrito para revisar
                    return;
                }

#if ANDROID || IOS
                HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
#endif

                // Generar número de pedido único
                var numeroPedido = $"CF{DateTime.Now:yyyyMMddHHmmss}";
                
                System.Diagnostics.Debug.WriteLine($"[INFO] FinalizarPedido - Generando pedido: {numeroPedido}");
                System.Diagnostics.Debug.WriteLine($"[INFO] FinalizarPedido - Total: {totalPedido:C}, Cantidad: {cantidadItems}");

                // ✅ SOLUCION: Copiar items a una lista temporal para pasarlos al ViewModel de confirmación
                var itemsDelCarrito = Items.Select(item => new ItemCarrito
                {
                    Producto = item.Producto,
                    Cantidad = item.Cantidad
                }).ToList();
                
                System.Diagnostics.Debug.WriteLine($"[INFO] FinalizarPedido - Items copiados a lista temporal: {itemsDelCarrito.Count}");
                
                // Guardar en variable estática temporal
                ConfirmacionPedidoViewModel.ItemsTemporales = itemsDelCarrito;

                // También serializar por si acaso
                var itemsJson = System.Text.Json.JsonSerializer.Serialize(itemsDelCarrito);
                System.Diagnostics.Debug.WriteLine($"[INFO] FinalizarPedido - JSON length: {itemsJson.Length}");

                // Preparar los parámetros de navegación
                var navigationParameter = new Dictionary<string, object>
                {
                    { "NumeroPedido", numeroPedido },
                    { "TotalString", totalPedido.ToString() },
                    { "CantidadItemsString", cantidadItems.ToString() },
                    { "ItemsJson", itemsJson },
                    { "Timestamp", DateTime.Now.Ticks.ToString() }
                };

                System.Diagnostics.Debug.WriteLine($"[INFO] FinalizarPedido - Navegando con parametros:");
                System.Diagnostics.Debug.WriteLine($"  - NumeroPedido: {numeroPedido}");
                System.Diagnostics.Debug.WriteLine($"  - Total: {totalPedido}");
                System.Diagnostics.Debug.WriteLine($"  - CantidadItems: {cantidadItems}");
                System.Diagnostics.Debug.WriteLine($"  - Items temporales: {itemsDelCarrito.Count}");
                
                // ✅ SOLUCIÓN: Volver a la raíz para limpiar el stack de navegación
                await Shell.Current.GoToAsync("//ListaProductosPage");
                
                System.Diagnostics.Debug.WriteLine($"[INFO] FinalizarPedido - Volvimos a la raíz");
                
                // ✅ IMPORTANTE: Navegar PRIMERO a la confirmación (con el carrito lleno)
                // para que el ViewModel pueda capturar los items
                await Shell.Current.GoToAsync("ConfirmacionPedidoPage", navigationParameter);
                
                System.Diagnostics.Debug.WriteLine($"[INFO] FinalizarPedido - Navegación completada");
                
                // ✅ DESPUÉS limpiar el carrito (ya se guardó el pedido con los items)
                _carritoService.LimpiarCarrito();
                
                System.Diagnostics.Debug.WriteLine($"[INFO] FinalizarPedido - Carrito limpiado, proceso completado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Finalizar pedido: {ex.Message}");
                await Shell.Current.DisplayAlertAsync("Error", "Hubo un problema al procesar tu pedido. Intentalo de nuevo.", "OK");
            }
        }

        private void CalcularTotal()
        {
            Total = _carritoService.GetTotal();
        }

        // ✅ NUEVO: Método público para actualizar/refrescar el carrito
        public void ActualizarCarrito()
        {
            System.Diagnostics.Debug.WriteLine($"[INFO] ActualizarCarrito llamado");
            System.Diagnostics.Debug.WriteLine($"[INFO] Items en servicio: {_carritoService.Items.Count}");
            System.Diagnostics.Debug.WriteLine($"[INFO] Items en ViewModel: {Items.Count}");
            
            // Como _items apunta directamente a _carritoService.Items, 
            // solo necesitamos recalcular el total y notificar cambios
            CalcularTotal();
            
            // Forzar actualización de la UI notificando cambio en la propiedad Items
            OnPropertyChanged(nameof(Items));
            
            System.Diagnostics.Debug.WriteLine($"[INFO] Total calculado: {Total:C}");
        }
    }
}