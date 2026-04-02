using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class EmpleadoPedidosViewModel : ObservableObject
{
    private readonly ApiService _api;
    private List<PedidoDto> _todos = new();

    public EmpleadoPedidosViewModel(ApiService api)
    {
        _api = api;
        WeakReferenceMessenger.Default.Register<NuevoPedidoMessage>(this, (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => CargarCommand.Execute(null)));
    }

    private string _filtroEstado = string.Empty;
    public string FiltroEstado
    {
        get => _filtroEstado;
        set
        {
            if (SetProperty(ref _filtroEstado, value))
            {
                AplicarFiltro();
                OnPropertyChanged(nameof(FiltroEnCursoActivo));
                OnPropertyChanged(nameof(FiltroPendienteActivo));
                OnPropertyChanged(nameof(FiltroEnPrepActivo));
                OnPropertyChanged(nameof(FiltroListoActivo));
                OnPropertyChanged(nameof(FiltroEntregadoActivo));
                OnPropertyChanged(nameof(FiltroCanceladoActivo));
            }
        }
    }

    public bool FiltroEnCursoActivo   => FiltroEstado == "";
    public bool FiltroPendienteActivo => FiltroEstado == "Pendiente";
    public bool FiltroEnPrepActivo    => FiltroEstado == "EnPreparacion";
    public bool FiltroListoActivo     => FiltroEstado == "Listo";
    public bool FiltroEntregadoActivo => FiltroEstado == "Entregado";
    public bool FiltroCanceladoActivo => FiltroEstado == "Cancelado";

    // ── Filtro por fecha ──────────────────────────────────────────────────────
    private string _filtroFecha = "Hoy";
    public string FiltroFecha
    {
        get => _filtroFecha;
        set
        {
            if (SetProperty(ref _filtroFecha, value))
            {
                OnPropertyChanged(nameof(FiltroHoyActivo));
                OnPropertyChanged(nameof(FiltroTodoActivo));
                AplicarFiltro();
            }
        }
    }

    public bool FiltroHoyActivo  => FiltroFecha == "Hoy";
    public bool FiltroTodoActivo => FiltroFecha == "Todo";

    [RelayCommand] private void SetFiltroFecha(string f) => FiltroFecha = f;

    public ObservableCollection<PedidoDto> Pedidos { get; } = new();

    [RelayCommand]
    public async Task CargarAsync()
    {
        try
        {
            _todos = await _api.GetHistorialStaffAsync();
            AplicarFiltro();
        }
        catch { /* ignorar errores de red */ }
    }

    [RelayCommand]
    private void SetFiltro(string estado) => FiltroEstado = estado;

    private void AplicarFiltro()
    {
        Pedidos.Clear();
        var hoy = DateTime.Now.Date;
        var filtrados = _todos.AsEnumerable();

        if (FiltroFecha == "Hoy")
            filtrados = filtrados.Where(p => p.FechaCreacion.ToLocalTime().Date == hoy);

        if (string.IsNullOrEmpty(FiltroEstado))
            filtrados = filtrados.Where(p => p.Estado == EstadoPedido.Pendiente || p.Estado == EstadoPedido.EnPreparacion);
        else
            filtrados = filtrados.Where(p => p.Estado.ToString() == FiltroEstado);

        foreach (var p in filtrados) Pedidos.Add(p);
    }

    [RelayCommand]
    private async Task PrepararAsync(PedidoDto pedido)
    {
        var ok = await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.EnPreparacion);
        if (ok)
            await Toast.Make($"Pedido #{pedido.NumeroPedido:D3} en preparación 🍳").Show();
        else
            await Shell.Current.DisplayAlert("Error", "No se pudo cambiar el estado del pedido.", "OK");
        await CargarAsync();
    }

    [RelayCommand]
    private async Task ListoAsync(PedidoDto pedido)
    {
        var ok = await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.Listo);
        if (ok)
            await Toast.Make($"Pedido #{pedido.NumeroPedido:D3} listo ✅").Show();
        else
            await Shell.Current.DisplayAlert("Error", "No se pudo marcar el pedido como listo.", "OK");
        await CargarAsync();
    }

    [RelayCommand]
    private async Task EntregarAsync(PedidoDto pedido)
    {
        var ok = await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.Entregado);
        if (ok)
            await Toast.Make($"Pedido #{pedido.NumeroPedido:D3} entregado 🎉").Show();
        else
            await Shell.Current.DisplayAlert("Error", "No se pudo marcar el pedido como entregado.", "OK");
        await CargarAsync();
    }

    [RelayCommand]
    private async Task CancelarAsync(PedidoDto pedido)
    {
        var confirmar = await Shell.Current.DisplayAlert(
            "Cancelar pedido", $"¿Cancelar el pedido #{pedido.NumeroPedido:D3}?", "Sí, cancelar", "No");
        if (!confirmar) return;
        var ok = await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.Cancelado);
        if (ok)
            await Toast.Make($"Pedido #{pedido.NumeroPedido:D3} cancelado").Show();
        else
            await Shell.Current.DisplayAlert("Error", "No se pudo cancelar el pedido. Inténtalo de nuevo.", "OK");
        await CargarAsync();
    }

    /// <summary>FIX-11: Limpia suscripciones de mensajes para evitar memory leaks.</summary>
    public void Cleanup() => WeakReferenceMessenger.Default.UnregisterAll(this);

    /// <summary>BUG-4: Restaura suscripciones al volver a la página (tab cacheado).</summary>
    public void Resubscribe()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        WeakReferenceMessenger.Default.Register<NuevoPedidoMessage>(this, (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => CargarCommand.Execute(null)));
    }
}
