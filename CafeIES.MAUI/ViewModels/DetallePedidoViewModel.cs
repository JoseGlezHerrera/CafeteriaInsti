using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

[QueryProperty(nameof(PedidoId), "pedidoId")]
public partial class DetallePedidoViewModel : ObservableObject
{
    private readonly ApiService _api;

    public DetallePedidoViewModel(ApiService api)
    {
        _api = api;
        WeakReferenceMessenger.Default.Register<PedidoActualizadoMessage>(this, (r, msg) =>
        {
            if (msg.PedidoId == ((DetallePedidoViewModel)r).PedidoId)
                MainThread.BeginInvokeOnMainThread(async () => await CargarAsync());
        });
    }

    [ObservableProperty] private int _pedidoId;
    [ObservableProperty] private int _numeroPedido;
    [ObservableProperty] private decimal _total;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoEmoji))]
    [NotifyPropertyChangedFor(nameof(EstadoTexto))]
    [NotifyPropertyChangedFor(nameof(EstadoColor))]
    [NotifyPropertyChangedFor(nameof(EstadoDescripcion))]
    [NotifyPropertyChangedFor(nameof(Paso2Color))]
    [NotifyPropertyChangedFor(nameof(Paso3Color))]
    [NotifyPropertyChangedFor(nameof(Paso4Color))]
    private EstadoPedido _estado;

    public ObservableCollection<LineaPedidoDto> Lineas { get; } = new();

    public string EstadoEmoji => Estado switch
    {
        EstadoPedido.Pendiente => "🧾",
        EstadoPedido.EnPreparacion => "👨‍🍳",
        EstadoPedido.Listo => "🔔",
        EstadoPedido.Entregado => "✅",
        _ => "❌"
    };

    public string EstadoTexto => Estado switch
    {
        EstadoPedido.Pendiente => "Pedido recibido",
        EstadoPedido.EnPreparacion => "Preparando tu pedido",
        EstadoPedido.Listo => "¡Listo para recoger!",
        EstadoPedido.Entregado => "Entregado",
        _ => "Cancelado"
    };

    public Color EstadoColor => Estado switch
    {
        EstadoPedido.Pendiente     => Color.FromArgb("#f5a623"),
        EstadoPedido.EnPreparacion => Color.FromArgb("#e8834a"),
        EstadoPedido.Listo         => Color.FromArgb("#4caf82"),
        EstadoPedido.Entregado     => Color.FromArgb("#4caf82"),
        _                          => Color.FromArgb("#e05252")
    };

    public string EstadoDescripcion => Estado switch
    {
        EstadoPedido.Pendiente     => "Tu pedido ha sido recibido y será preparado en breve.",
        EstadoPedido.EnPreparacion => "La cafetería está preparando tu pedido.",
        EstadoPedido.Listo         => "Acércate al mostrador a recogerlo.",
        EstadoPedido.Entregado     => "Pedido entregado correctamente.",
        _                          => "Este pedido fue cancelado."
    };

    private static readonly Color _accentColor = Color.FromArgb("#f5a623");
    private static readonly Color _dimColor    = Color.FromArgb("#2e2b26");

    public Color Paso2Color => Estado >= EstadoPedido.EnPreparacion ? _accentColor : _dimColor;
    public Color Paso3Color => Estado >= EstadoPedido.Listo         ? _accentColor : _dimColor;
    public Color Paso4Color => Estado >= EstadoPedido.Entregado     ? _accentColor : _dimColor;

    partial void OnPedidoIdChanged(int value) => _ = CargarAsync(value);

    [RelayCommand]
    public async Task CargarAsync(int id = 0)
    {
        if (id == 0) id = PedidoId;
        if (id <= 0) return;

        // BUG-045: evitar cargas concurrentes (SignalR + llamada directa)
        // No usamos IsLoading porque no hay spinner en esta vista, solo guardamos
        try
        {
            var pedido = await _api.GetPedidoAsync(id);
            if (pedido is null) return;

            NumeroPedido = pedido.NumeroPedido;
            Total        = pedido.Total;
            Estado       = pedido.Estado;
            Lineas.Clear();
            foreach (var l in pedido.Lineas) Lineas.Add(l);
        }
        catch { /* ignorar errores de red — la vista conserva el último estado */ }
    }

    [RelayCommand]
    private async Task VolverAsync() => await Shell.Current.GoToAsync("..");

    public void Cleanup() => WeakReferenceMessenger.Default.Unregister<PedidoActualizadoMessage>(this);
}