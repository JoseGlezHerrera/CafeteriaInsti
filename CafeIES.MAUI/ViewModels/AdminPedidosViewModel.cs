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
    // BUG-2+5: Lista backing para filtrado client-side sin recargar servidor
    private List<PedidoDto> _todos = new();
    // Evita que dos llamadas concurrentes a CargarAsync() dupliquen los institutos
    private bool _institutosCargados;

    public AdminPedidosViewModel(ApiService api)
    {
        _api = api;
        WeakReferenceMessenger.Default.Register<NuevoPedidoMessage>(this, (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => CargarCommand.Execute(null)));
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isCargandoMas;
    [ObservableProperty] private bool _hayMas;

    // ── Filtro por estado (client-side sobre todos los cargados) ─────────────
    private string _filtroEstado = string.Empty;
    public string FiltroEstado
    {
        get => _filtroEstado;
        set
        {
            if (SetProperty(ref _filtroEstado, value))
            {
                AplicarFiltro();
                OnPropertyChanged(nameof(FiltroTodosActivo));
                OnPropertyChanged(nameof(FiltroPendienteActivo));
                OnPropertyChanged(nameof(FiltroEnPrepActivo));
                OnPropertyChanged(nameof(FiltroListoActivo));
                OnPropertyChanged(nameof(FiltroEntregadoActivo));
                OnPropertyChanged(nameof(FiltroCanceladoActivo));
            }
        }
    }

    public bool FiltroTodosActivo     => FiltroEstado == "";
    public bool FiltroPendienteActivo => FiltroEstado == "Pendiente";
    public bool FiltroEnPrepActivo    => FiltroEstado == "EnPreparacion";
    public bool FiltroListoActivo     => FiltroEstado == "Listo";
    public bool FiltroEntregadoActivo => FiltroEstado == "Entregado";
    public bool FiltroCanceladoActivo => FiltroEstado == "Cancelado";

    // ── Filtro por fecha (server-side: recarga al cambiar) ────────────────────
    private string _filtroFecha = "Hoy";
    public string FiltroFecha
    {
        get => _filtroFecha;
        set
        {
            if (SetProperty(ref _filtroFecha, value))
            {
                OnPropertyChanged(nameof(FiltroHoyActivo));
                OnPropertyChanged(nameof(FiltroSemanaActivo));
                OnPropertyChanged(nameof(FiltroTodoActivo));
                _ = CargarAsync();
            }
        }
    }

    public bool FiltroHoyActivo    => FiltroFecha == "Hoy";
    public bool FiltroSemanaActivo => FiltroFecha == "Semana";
    public bool FiltroTodoActivo   => FiltroFecha == "Todo";

    [RelayCommand] private void SetFiltroFecha(string f) => FiltroFecha = f;

    // ── Filtro por instituto (server-side al recargar) ────────────────────────
    public ObservableCollection<InstitutoDto> Institutos { get; } = new();

    private InstitutoDto? _filtroInstituto;
    public InstitutoDto? FiltroInstituto
    {
        get => _filtroInstituto;
        set { if (SetProperty(ref _filtroInstituto, value)) _ = CargarAsync(); }
    }

    public ObservableCollection<PedidoDto> Pedidos { get; } = new();

    private void AplicarFiltro()
    {
        Pedidos.Clear();
        var filtrados = string.IsNullOrEmpty(_filtroEstado)
            ? _todos
            : _todos.Where(p => p.Estado.ToString() == _filtroEstado).ToList();
        foreach (var p in filtrados) Pedidos.Add(p);
    }

    [RelayCommand]
    public async Task CargarAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        _todos.Clear();
        _paginaActual = 1;

        // Carga institutos la primera vez.
        // _institutosCargados se fija ANTES del await para que si una segunda llamada concurrente
        // entra mientras esperamos la respuesta HTTP, no vuelva a añadir los institutos.
        if (!_institutosCargados)
        {
            _institutosCargados = true;
            try
            {
                var institutos = await _api.GetInstitutosAsync();
                Institutos.Clear();
                Institutos.Add(new InstitutoDto(0, "Todos los centros", "", true));
                foreach (var i in institutos) Institutos.Add(i);
                if (_filtroInstituto is null) _filtroInstituto = Institutos[0];
            }
            catch
            {
                // BUG-015: resetear el flag para permitir reintento en la próxima llamada
                _institutosCargados = false;
            }
        }

        var institutoId = _filtroInstituto?.Id > 0 ? _filtroInstituto.Id : (int?)null;
        var desde       = DesdeParaFiltro();
        var result = await _api.GetPedidosAdminPaginadoAsync(page: 1, pageSize: PageSize, institutoId: institutoId, desde: desde);
        if (result is not null)
        {
            _totalCount = result.TotalCount;
            foreach (var p in result.Items) _todos.Add(p);
            HayMas = _todos.Count < _totalCount;
        }
        AplicarFiltro();
        IsLoading = false;
    }

    // BUG-022: usar zona horaria España para calcular medianoche local;
    // DateTime.UtcNow.Date es medianoche UTC (≠ medianoche España en UTC+1/+2)
    // y perdería las órdenes de las primeras 1–2 horas del día.
    private static readonly TimeZoneInfo _spainTz = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Madrid");

    private DateTime? DesdeParaFiltro()
    {
        var hoyEsp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _spainTz).Date;
        return FiltroFecha switch
        {
            "Hoy"    => TimeZoneInfo.ConvertTimeToUtc(hoyEsp, _spainTz),
            "Semana" => TimeZoneInfo.ConvertTimeToUtc(hoyEsp.AddDays(-6), _spainTz),
            _        => null
        };
    }

    [RelayCommand]
    private async Task CargarMasAsync()
    {
        if (IsCargandoMas || !HayMas) return;
        IsCargandoMas = true;
        _paginaActual++;
        var institutoId = _filtroInstituto?.Id > 0 ? _filtroInstituto.Id : (int?)null;
        var result = await _api.GetPedidosAdminPaginadoAsync(page: _paginaActual, pageSize: PageSize, institutoId: institutoId, desde: DesdeParaFiltro());
        if (result is not null)
        {
            foreach (var p in result.Items) _todos.Add(p);
            HayMas = _todos.Count < _totalCount;
        }
        AplicarFiltro();
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

    /// <summary>BUG-4: Restaura suscripciones al volver a la página (tab cacheado).</summary>
    public void Resubscribe()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        WeakReferenceMessenger.Default.Register<NuevoPedidoMessage>(this, (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => CargarCommand.Execute(null)));
    }
}
