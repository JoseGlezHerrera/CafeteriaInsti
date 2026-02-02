// ViewModels/ConfirmacionPedidoViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeteriaInsti.ViewModels
{
    [QueryProperty(nameof(NumeroPedido), nameof(NumeroPedido))]
    [QueryProperty(nameof(TotalString), nameof(TotalString))]
    [QueryProperty(nameof(CantidadItemsString), nameof(CantidadItemsString))]
    [QueryProperty(nameof(Timestamp), nameof(Timestamp))] // Nuevo parámetro para forzar navegación fresca
    public partial class ConfirmacionPedidoViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string _numeroPedido = string.Empty;

        [ObservableProperty]
        private decimal _total;

        [ObservableProperty]
        private int _cantidadItems;

        [ObservableProperty]
        private DateTime _horaEstimada;
        
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

        public ConfirmacionPedidoViewModel()
        {
            Title = "Confirmación";
            HoraEstimada = DateTime.Now.AddMinutes(15);
            System.Diagnostics.Debug.WriteLine("[INFO] ConfirmacionPedido - Constructor llamado");
            System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - Valores iniciales: NumeroPedido={NumeroPedido}, Total={Total:C}, CantidadItems={CantidadItems}");
        }

        // Este método se llama automáticamente cuando NumeroPedido cambia (gracias a ObservableProperty)
        partial void OnNumeroPedidoChanged(string value)
        {
            System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - NumeroPedido cambió a: {value}");
            // Actualizar la hora estimada cada vez que se recibe un nuevo número de pedido
            HoraEstimada = DateTime.Now.AddMinutes(15);
            System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedido - HoraEstimada actualizada: {HoraEstimada:HH:mm}");
        }

        [RelayCommand]
        private async Task CompartirPedidoAsync()
        {
            try
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = "Mi Pedido - Cafetería Insti",
                    Text = $"He realizado un pedido en la Cafetería!\n\n" +
                           $"Pedido: #{NumeroPedido}\n" +
                           $"Total: {Total:C}\n" +
                           $"Artículos: {CantidadItems}\n" +
                           $"Recogida estimada: {HoraEstimada:HH:mm}"
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
