using CafeIES.MAUI.Services;
using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class PagamentoWebPage : ContentPage
{
    private readonly CarritoViewModel _carrito;
    private readonly ApiService       _api;
    private bool _procesando;

    public PagamentoWebPage(CarritoViewModel carrito, ApiService api)
    {
        InitializeComponent();
        _carrito = carrito;
        _api     = api;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Si el estado de pago fue cancelado (p. ej. el usuario cambió de tab y volvió),
        // sacamos esta página del stack y el usuario debe reiniciar el pedido desde el carrito.
        if (string.IsNullOrEmpty(_carrito.PendingClientSecret))
        {
            Dispatcher.Dispatch(async () => await Shell.Current.GoToAsync(".."));
            return;
        }

        _procesando = false;
        LoadingOverlay.IsVisible = true;

        var url = $"{_api.ApiBaseUrl}/api/pagos/stripe-form"
                + $"?pk={Uri.EscapeDataString(_carrito.PendingPublishableKey)}"
                + $"&cs={Uri.EscapeDataString(_carrito.PendingClientSecret)}";

        FormWebView.Source = new UrlWebViewSource { Url = url };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Si el usuario sale sin completar el pago (back, cambio de tab, etc.),
        // cancelamos el intent pendiente para que el próximo "Confirmar" cree uno nuevo.
        if (!_procesando)
            _carrito.CancelarPendingPago();
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        LoadingOverlay.IsVisible = false;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (_procesando) { e.Cancel = true; return; }

        if (e.Url.StartsWith("cafeies://success/", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            _procesando = true;
            var piId = e.Url["cafeies://success/".Length..];
            MainThread.BeginInvokeOnMainThread(async () => await HandleSuccessAsync(piId));
        }
    }

    private async Task HandleSuccessAsync(string piId)
    {
        LoadingLabel.Text = "Procesando pedido…";
        LoadingOverlay.IsVisible = true;

        // Volver a CarritoPage primero (saca PagamentoWebPage del stack)
        // OnDisappearing se disparará pero _procesando = true así que NO cancela el estado
        await Shell.Current.GoToAsync("..");

        // Luego finalizar: crea el pedido y navega a ConfirmacionPedido
        await _carrito.FinalizarPagoAsync(piId);
    }

    // Bloquear el botón "←" mientras se procesa el pago para evitar doble navegación
    private async void OnVolverClicked(object? sender, EventArgs e)
    {
        if (_procesando) return;
        await Shell.Current.GoToAsync("..");
    }

    // Bloquear el botón físico atrás de Android mientras se procesa el pago
    protected override bool OnBackButtonPressed()
    {
        if (_procesando) return true; // true = no hacer nada
        return base.OnBackButtonPressed();
    }
}
