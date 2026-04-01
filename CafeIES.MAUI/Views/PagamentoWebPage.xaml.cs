using CafeIES.MAUI.Services;
using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class PagamentoWebPage : ContentPage
{
    private readonly CarritoViewModel _carrito;
    private readonly ApiService       _api;
    // 0 = libre, 1 = procesando. Usar Interlocked para evitar race condition
    // si OnNavigating se dispara desde hilos distintos en Android.
    private int _procesando;

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

        Interlocked.Exchange(ref _procesando, 0);
        LoadingOverlay.IsVisible = true;

        // BUG-E: pk ya no se pasa en la URL — el servidor lo inyecta desde configuración.
        var url = $"{_api.ApiBaseUrl}/api/pagos/stripe-form"
                + $"?cs={Uri.EscapeDataString(_carrito.PendingClientSecret)}";

        FormWebView.Source = new UrlWebViewSource { Url = url };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Si el usuario sale sin completar el pago (back, cambio de tab, etc.),
        // cancelamos el intent pendiente para que el próximo "Confirmar" cree uno nuevo.
        if (Volatile.Read(ref _procesando) == 0)
            _carrito.CancelarPendingPago();
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        LoadingOverlay.IsVisible = false;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith("cafeies://success/", StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;

        // Interlocked.CompareExchange: solo el primer hilo que cambie 0→1 procesa el pago.
        // Evita duplicados si OnNavigating se dispara dos veces antes de que el flag se actualice.
        if (Interlocked.CompareExchange(ref _procesando, 1, 0) != 0) return;

        var piId = e.Url["cafeies://success/".Length..];
        MainThread.BeginInvokeOnMainThread(async () => await HandleSuccessAsync(piId));
    }

    private async Task HandleSuccessAsync(string piId)
    {
        // Navegar a confirmación inmediatamente — la creación del pedido ocurre en background.
        // No hay "Creando pedido…": el pago ya fue cobrado, el usuario no debe esperar.
        await _carrito.FinalizarPagoAsync(piId);
    }

    // Botón "←" de la UI: bloquea mientras procesa; muestra confirmación en otro caso
    private async void OnVolverClicked(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _procesando) != 0) return;
        await MostrarDialogoCancelarAsync();
    }

    // Botón físico atrás de Android: bloquea mientras procesa; confirmación en otro caso
    protected override bool OnBackButtonPressed()
    {
        if (Volatile.Read(ref _procesando) != 0) return true; // bloquear durante pago
        _ = MostrarDialogoCancelarAsync();
        return true; // interceptar siempre — el diálogo decide si navegar
    }

    private async Task MostrarDialogoCancelarAsync()
    {
        var confirmar = await DisplayAlert(
            "Cancelar pago",
            "¿Quieres cancelar el pago y volver al carrito?",
            "Sí, cancelar", "No, continuar");
        if (confirmar)
            await Shell.Current.GoToAsync("..");
    }
}
