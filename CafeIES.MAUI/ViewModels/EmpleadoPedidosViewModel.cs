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
            MainThread.BeginInvokeOnMainThread(async () => await CargarAsync()));
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
        await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.EnPreparacion);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task ListoAsync(PedidoDto pedido)
    {
        await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.Listo);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task EntregarAsync(PedidoDto pedido)
    {
        await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.Entregado);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task CancelarAsync(PedidoDto pedido)
    {
        var confirmar = await Shell.Current.DisplayAlert(
            "Cancelar pedido", $"¿Cancelar el pedido #{pedido.NumeroPedido:D3}?", "Sí, cancelar", "No");
        if (!confirmar) return;
        await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.Cancelado);
        await CargarAsync();
    }
}
