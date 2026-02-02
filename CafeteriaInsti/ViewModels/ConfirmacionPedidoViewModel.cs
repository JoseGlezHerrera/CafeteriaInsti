// ViewModels/ConfirmacionPedidoViewModel.cs
using CafeteriaInsti.Models;
using CafeteriaInsti.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeteriaInsti.ViewModels
{
    [QueryProperty(nameof(NumeroPedido), nameof(NumeroPedido))]
    [QueryProperty(nameof(TotalString), nameof(TotalString))]
    [QueryProperty(nameof(CantidadItemsString), nameof(CantidadItemsString))]
    [QueryProperty(nameof(ItemsJson), nameof(ItemsJson))]
    [QueryProperty(nameof(Timestamp), nameof(Timestamp))] // Parámetro para forzar navegación fresca
    public partial class ConfirmacionPedidoViewModel : BaseViewModel
    {
        private readonly PedidoService _pedidoService;
        private readonly CarritoService _carritoService;
        
        // ? Variable estática temporal para pasar los items entre ViewModels
        public static List<ItemCarrito>? ItemsTemporales { get; set; }

        [ObservableProperty]
        private string _numeroPedido = string.Empty;

        [ObservableProperty]
        private decimal _total;

        [ObservableProperty]
        private int _cantidadItems;
        
        // Items del carrito como JSON
        public string ItemsJson { get; set; } = string.Empty;
        
        // Parámetro auxiliar para forzar navegación fresca (no se usa en la UI)
        public string Timestamp { get; set; } = string.Empty;

        public string TotalString
        {
            get => Total.ToString();
            set
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - TotalString recibido: {value}");
                if (decimal.TryParse(value, out var result))
                {
                    Total = result;
                    System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - Total asignado: {Total:C}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WARNING] ConfirmacionPedido - No se pudo parsear TotalString: {value}");
                }
            }
        }

        public string CantidadItemsString
        {
            get => CantidadItems.ToString();
            set
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - CantidadItemsString recibido: {value}");
                if (int.TryParse(value, out var result))
                {
                    CantidadItems = result;
                    System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - CantidadItems asignado: {CantidadItems}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WARNING] ConfirmacionPedido - No se pudo parsear CantidadItemsString: {value}");
                }
            }
        }

        public ConfirmacionPedidoViewModel(PedidoService pedidoService, CarritoService carritoService)
        {
            Title = "Confirmacion";
            _pedidoService = pedidoService;
            _carritoService = carritoService;
            System.Diagnostics.Debug.WriteLine("[INFO] ConfirmacionPedido - Constructor llamado");
            System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - Valores iniciales: NumeroPedido={NumeroPedido}, Total={Total:C}, CantidadItems={CantidadItems}");
        }

        // Este método se llama automáticamente cuando NumeroPedido cambia (gracias a ObservableProperty)
        partial void OnNumeroPedidoChanged(string value)
        {
            System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - NumeroPedido cambió a: {value}");
            System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - ItemsJson length: {ItemsJson?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - ItemsJson content: {ItemsJson}");
            
            if (string.IsNullOrEmpty(value)) return;
            
            // ? IMPORTANTE: Guardar el pedido INMEDIATAMENTE cuando recibimos el número
            // porque el carrito se va a limpiar después
            GuardarPedidoEnHistorial();
        }

        private void GuardarPedidoEnHistorial()
        {
            try
            {
                var itemsDelCarrito = new List<ItemCarrito>();
                
                // ? SOLUCION DEFINITIVA: Primero intentar usar los items temporales estáticos
                if (ItemsTemporales != null && ItemsTemporales.Any())
                {
                    itemsDelCarrito = ItemsTemporales;
                    System.Diagnostics.Debug.WriteLine($"[INFO] GuardarPedidoEnHistorial - Items de variable temporal: {itemsDelCarrito.Count}");
                }
                // Si no, intentar deserializar del JSON
                else if (!string.IsNullOrEmpty(ItemsJson))
                {
                    itemsDelCarrito = System.Text.Json.JsonSerializer.Deserialize<List<ItemCarrito>>(ItemsJson) ?? new List<ItemCarrito>();
                    System.Diagnostics.Debug.WriteLine($"[INFO] GuardarPedidoEnHistorial - Items deserializados del JSON: {itemsDelCarrito.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] GuardarPedidoEnHistorial - No hay items disponibles!");
                }

                // Crear el pedido con toda la información
                var pedido = new Pedido
                {
                    NumeroPedido = NumeroPedido,
                    FechaPedido = DateTime.Now,
                    Items = itemsDelCarrito
                    // ? Total y CantidadItems se calculan automáticamente desde Items
                };

                _pedidoService.GuardarPedido(pedido);
                System.Diagnostics.Debug.WriteLine($"[INFO] Pedido guardado en historial: {NumeroPedido} con {itemsDelCarrito.Count} items");
                
                // Limpiar la variable temporal
                ItemsTemporales = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error al guardar pedido en historial: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        [RelayCommand]
        private async Task CompartirPedidoAsync()
        {
            try
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = "Mi Pedido - Cafeteria Insti",
                    Text = $"He realizado un pedido en la Cafeteria!\n\n" +
                           $"Pedido: #{NumeroPedido}\n" +
                           $"Total: {Total:C}\n" +
                           $"Articulos: {CantidadItems}"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Compartir pedido: {ex.Message}");
                await Shell.Current.DisplayAlertAsync("Error", "No se pudo compartir el pedido", "OK");
            }
        }

        [RelayCommand]
        private async Task VolverAlMenuAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("//ListaProductosPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Navegar al menú: {ex.Message}");
            }
        }
    }
}
