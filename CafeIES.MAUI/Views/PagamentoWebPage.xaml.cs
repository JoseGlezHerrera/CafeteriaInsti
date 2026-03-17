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
        _procesando = false;
        LoadingOverlay.IsVisible = true;

        var url = $"{_api.ApiBaseUrl}/api/pagos/stripe-form"
                + $"?pk={Uri.EscapeDataString(_carrito.PendingPublishableKey)}"
                + $"&cs={Uri.EscapeDataString(_carrito.PendingClientSecret)}";

        FormWebView.Source = new UrlWebViewSource { Url = url };
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
        await Shell.Current.GoToAsync("..");

        // Luego finalizar: crea el pedido y navega a ConfirmacionPedido
        await _carrito.FinalizarPagoAsync(piId);
    }

    private async void OnVolverClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");
}
