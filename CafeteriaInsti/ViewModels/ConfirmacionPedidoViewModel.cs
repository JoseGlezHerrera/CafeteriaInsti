// ViewModels/ConfirmacionPedidoViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeteriaInsti.ViewModels
{
    [QueryProperty(nameof(NumeroPedido), nameof(NumeroPedido))]
    [QueryProperty(nameof(TotalString), nameof(TotalString))]
    [QueryProperty(nameof(CantidadItemsString), nameof(CantidadItemsString))]
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

        public string TotalString
        {
            get => Total.ToString();
            set
            {
                if (decimal.TryParse(value, out var result))
                {
                    Total = result;
                }
            }
        }

        public string CantidadItemsString
        {
            get => CantidadItems.ToString();
            set
            {
                if (int.TryParse(value, out var result))
                {
                    CantidadItems = result;
                }
            }
        }

        public ConfirmacionPedidoViewModel()
        {
            Title = "Confirmación";
            HoraEstimada = DateTime.Now.AddMinutes(15);
        }

        [RelayCommand]
        private async Task CompartirPedidoAsync()
        {
            try
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = "Mi Pedido - Cafetería Insti",
                    Text = $"¡He realizado un pedido en la Cafetería! ??\n\n" +
                           $"?? Pedido: #{NumeroPedido}\n" +
                           $"?? Total: {Total:C}\n" +
                           $"?? Artículos: {CantidadItems}\n" +
                           $"? Recogida estimada: {HoraEstimada:HH:mm}"
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
