using System.Globalization;
using CafeIES.MAUI.Services;

namespace CafeIES.MAUI.Views;

[QueryProperty(nameof(PaymentIntentId), "paymentIntentId")]
[QueryProperty(nameof(Total),           "total")]
public partial class ConfirmacionPedidoPage : ContentPage
{
    private readonly ApiService _api;
    private string _paymentIntentId = string.Empty;
    private CancellationTokenSource? _pollCts;
    // FIX-BK: permitir volver cuando el polling haya terminado (éxito o timeout)
    private bool _pagoCompletado;

    public ConfirmacionPedidoPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    public string PaymentIntentId
    {
        set => _paymentIntentId = value ?? string.Empty;
    }

    public string Total
    {
        set
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                TotalLabel.Text = $"Total: {amount:F2}€";
            else
                TotalLabel.Text = $"Total: {value}€";
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (string.IsNullOrEmpty(_paymentIntentId)) return;

        NumeroPedidoLabel.Text = "…";

        // Pedidos gratuitos incluyen el NumeroPedido en el token (formato "gratuito-{numero}")
        if (_paymentIntentId.StartsWith("gratuito-", StringComparison.OrdinalIgnoreCase))
        {
            var numeroStr = _paymentIntentId["gratuito-".Length..];
            NumeroPedidoLabel.Text = int.TryParse(numeroStr, out var num) ? $"#{num:D3}" : numeroStr;
            _pagoCompletado = true;
            return;
        }

        _pagoCompletado = false;
        _pollCts = new CancellationTokenSource();
        _ = PollNumeroPedidoAsync(_pollCts.Token);
    }


    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _pollCts?.Cancel();
        _pollCts = null;
    }

    /// <summary>Sondea el servidor hasta obtener el número de pedido o hasta agotar el tiempo.</summary>
    private async Task PollNumeroPedidoAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            await Task.Delay(2000, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            var pedido = await _api.GetPedidoByIntentAsync(_paymentIntentId);
            if (pedido is not null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    NumeroPedidoLabel.Text = $"#{pedido.NumeroPedido:D3}";
                    _pagoCompletado = true;
                });
                return;
            }
        }
        // Timeout: el pedido se creará igual (webhook), solo no tenemos el número ahora.
        // Informar al usuario en lugar de dejar "…" indefinidamente.
        if (!ct.IsCancellationRequested)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                NumeroPedidoLabel.Text = "Revisa tu\nhistorial";
                _pagoCompletado = true;
            });
    }

    protected override bool OnBackButtonPressed()
    {
        // Permitir volver cuando el pago ya ha concluido (éxito o timeout de polling)
        if (_pagoCompletado)
            return base.OnBackButtonPressed();
        // Pago Stripe aún en curso — bloquear para no volver a la pasarela
        return true;
    }

    private async void OnVerPedidosClicked(object sender, EventArgs e)
    {
        // Primero sacar esta página del stack del tab Carrito,
        // así al volver al carrito el usuario ve el carrito vacío (no esta pantalla de nuevo).
        await Shell.Current.GoToAsync("..");
        await Shell.Current.GoToAsync("//Main/Pedidos");
    }

    private async void OnSeguirPidiendoClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
        await Shell.Current.GoToAsync("//Main/Inicio");
    }
}
