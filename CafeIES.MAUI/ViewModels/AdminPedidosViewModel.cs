using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class AdminPedidosViewModel : ObservableObject
{
    private readonly ApiService _api;
    private List<PedidoDto> _todos = new();

    public AdminPedidosViewModel(ApiService api)
    {
        _api = api;
        WeakReferenceMessenger.Default.Register<NuevoPedidoMessage>(this, (_, _) =>
            MainThread.BeginInvokeOnMainThread(async () => await CargarAsync()));
    }

    [ObservableProperty] private bool _isLoading;

    // ── Filtro por estado (client-side) ───────────────────────────────────────
    private string _filtroEstado = string.Empty;
    public string FiltroEstado
    {
        get => _filtroEstado;
        set { if (SetProperty(ref _filtroEstado, value)) AplicarFiltro(); }
    }

    // ── Filtro por instituto (server-side al recargar) ────────────────────────
    public ObservableCollection<InstitutoDto> Institutos { get; } = new();

    private InstitutoDto? _filtroInstituto;
    public InstitutoDto? FiltroInstituto
    {
        get => _filtroInstituto;
        set { if (SetProperty(ref _filtroInstituto, value)) _ = CargarAsync(); }
    }

    public ObservableCollection<PedidoDto> Pedidos { get; } = new();

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsLoading = true;

        // Carga institutos la primera vez
        if (Institutos.Count == 0)
        {
            var institutos = await _api.GetInstitutosAsync();
            Institutos.Add(new InstitutoDto(0, "Todos los centros", ""));
            foreach (var i in institutos) Institutos.Add(i);
            if (_filtroInstituto is null) _filtroInstituto = Institutos[0];
        }

        var institutoId = _filtroInstituto?.Id > 0 ? _filtroInstituto.Id : (int?)null;
        _todos = await _api.GetAllPedidosAsync(institutoId);
        AplicarFiltro();
        IsLoading = false;
    }

    [RelayCommand]
    private void SetFiltro(string estado)
    {
        FiltroEstado = estado;
    }

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
            "Cancelar pedido",
            $"¿Cancelar el pedido #{pedido.NumeroPedido:D3}? Se restaurará el stock.",
            "Sí, cancelar", "No");
        if (!confirmar) return;

        await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.Cancelado);
        await CargarAsync();
    }
}
