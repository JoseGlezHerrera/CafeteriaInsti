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
            return;
        }

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
                    NumeroPedidoLabel.Text = $"#{pedido.NumeroPedido:D3}");
                return;
            }
        }
        // Timeout: el pedido se creará igual (webhook), solo no tenemos el número ahora
    }

    protected override bool OnBackButtonPressed() => true; // no volver a la pasarela

    private async void OnVerPedidosClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("//Main/Pedidos");

    private async void OnSeguirPidiendoClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("//Main/Inicio");
}
