using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Controls;

/// <summary>
/// Tarjeta de pedido reutilizable para EmpleadoPedidosPage y AdminPedidosPage.
/// Expone eventos que las páginas padre enrutan a sus respectivos ViewModels.
/// </summary>
public partial class PedidoCardView : ContentView
{
    // ── Eventos de acción ─────────────────────────────────────────────────────
    public event EventHandler<PedidoDto>? PrepararRequested;
    public event EventHandler<PedidoDto>? ListoRequested;
    public event EventHandler<PedidoDto>? EntregarRequested;
    public event EventHandler<PedidoDto>? CancelarRequested;

    public PedidoCardView()
    {
        InitializeComponent();
    }

    private async void OnPrepararClicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            await AnimatePress(btn);
            if (btn.CommandParameter is PedidoDto p) PrepararRequested?.Invoke(this, p);
        }
    }

    private async void OnListoClicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            await AnimatePress(btn);
            if (btn.CommandParameter is PedidoDto p) ListoRequested?.Invoke(this, p);
        }
    }

    private async void OnEntregarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            await AnimatePress(btn);
            if (btn.CommandParameter is PedidoDto p) EntregarRequested?.Invoke(this, p);
        }
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            await AnimatePress(btn);
            if (btn.CommandParameter is PedidoDto p) CancelarRequested?.Invoke(this, p);
        }
    }

    private static async Task AnimatePress(VisualElement el)
    {
        await el.ScaleTo(0.88, 80, Easing.CubicIn);
        await el.ScaleTo(1.0,  80, Easing.CubicOut);
    }
}
