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

    [ObservableProperty] private bool _isLoading;

    private string _filtroEstado = string.Empty;
    public string FiltroEstado
    {
        get => _filtroEstado;
        set { if (SetProperty(ref _filtroEstado, value)) AplicarFiltro(); }
    }

    public ObservableCollection<PedidoDto> Pedidos { get; } = new();

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsLoading = true;
        _todos = await _api.GetPedidosEnCursoAsync();
        AplicarFiltro();
        IsLoading = false;
    }

    [RelayCommand]
    private void SetFiltro(string estado) => FiltroEstado = estado;

    private void AplicarFiltro()
    {
        Pedidos.Clear();
        var filtrados = string.IsNullOrEmpty(FiltroEstado)
            ? _todos
            : _todos.Where(p => p.Estado.ToString() == FiltroEstado).ToList();
        foreach (var p in filtrados) Pedidos.Add(p);
    }

    [RelayCommand]
    private async Task PrepararAsync(PedidoDto pedido)
    {
        // FIX-14: Verificar resultado y mostrar error si falla
        var ok = await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.EnPreparacion);
        if (!ok)
            await Shell.Current.DisplayAlert("Error", "No se pudo cambiar el estado del pedido.", "OK");
        await CargarAsync();
    }

    [RelayCommand]
    private async Task ListoAsync(PedidoDto pedido)
    {
        var ok = await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.Listo);
        if (!ok)
            await Shell.Current.DisplayAlert("Error", "No se pudo marcar el pedido como listo.", "OK");
        await CargarAsync();
    }

    [RelayCommand]
    private async Task EntregarAsync(PedidoDto pedido)
    {
        var ok = await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.Entregado);
        if (!ok)
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
        // FIX-14: Informar al usuario si falla
        if (!ok)
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
