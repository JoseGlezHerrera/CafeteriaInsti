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
    // FIX-19: Paginación real en lugar de cargar todos los pedidos
    private const int PageSize = 30;
    private int _paginaActual;
    private int _totalCount;

    public AdminPedidosViewModel(ApiService api)
    {
        _api = api;
        WeakReferenceMessenger.Default.Register<NuevoPedidoMessage>(this, (_, _) =>
            MainThread.BeginInvokeOnMainThread(async () => await CargarAsync()));
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isCargandoMas;
    [ObservableProperty] private bool _hayMas;

    // ── Filtro por estado (client-side sobre la página cargada) ──────────────
    private string _filtroEstado = string.Empty;
    public string FiltroEstado
    {
        get => _filtroEstado;
        set { if (SetProperty(ref _filtroEstado, value)) _ = CargarAsync(); }
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
        Pedidos.Clear();
        _paginaActual = 1;

        // Carga institutos la primera vez
        if (Institutos.Count == 0)
        {
            var institutos = await _api.GetInstitutosAsync();
            Institutos.Add(new InstitutoDto(0, "Todos los centros", ""));
            foreach (var i in institutos) Institutos.Add(i);
            if (_filtroInstituto is null) _filtroInstituto = Institutos[0];
        }

        var institutoId = _filtroInstituto?.Id > 0 ? _filtroInstituto.Id : (int?)null;
        var result = await _api.GetPedidosAdminPaginadoAsync(page: 1, pageSize: PageSize, institutoId: institutoId);
        if (result is not null)
        {
            _totalCount = result.TotalCount;
            foreach (var p in result.Items) Pedidos.Add(p);
            HayMas = Pedidos.Count < _totalCount;
        }
        IsLoading = false;
    }

    [RelayCommand]
    private async Task CargarMasAsync()
    {
        if (IsCargandoMas || !HayMas) return;
        IsCargandoMas = true;
        _paginaActual++;
        var institutoId = _filtroInstituto?.Id > 0 ? _filtroInstituto.Id : (int?)null;
        var result = await _api.GetPedidosAdminPaginadoAsync(page: _paginaActual, pageSize: PageSize, institutoId: institutoId);
        if (result is not null)
        {
            foreach (var p in result.Items) Pedidos.Add(p);
            HayMas = Pedidos.Count < _totalCount;
        }
        IsCargandoMas = false;
    }

    [RelayCommand]
    private void SetFiltro(string estado)
    {
        FiltroEstado = estado;
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
            "Cancelar pedido",
            $"¿Cancelar el pedido #{pedido.NumeroPedido:D3}? Se restaurará el stock.",
            "Sí, cancelar", "No");
        if (!confirmar) return;

        var ok = await _api.CambiarEstadoPedidoAsync(pedido.Id, EstadoPedido.Cancelado);
        if (!ok)
            await Shell.Current.DisplayAlert("Error", "No se pudo cancelar el pedido. Inténtalo de nuevo.", "OK");
        await CargarAsync();
    }

    /// <summary>FIX-11: Limpia suscripciones de mensajes para evitar memory leaks.</summary>
    public void Cleanup() => WeakReferenceMessenger.Default.UnregisterAll(this);
}
