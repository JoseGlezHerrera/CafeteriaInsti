using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

// ── PedidosViewModel ─────────────────────────────────────────────────────────

public partial class PedidosViewModel : ObservableObject
{
    private const int PageSize = 20;

    private readonly ApiService _api;
    private int _paginaActual;
    private bool _hayMasServidor;
    private List<PedidoDto> _todos = new();

    public PedidosViewModel(ApiService api)
    {
        _api = api;
        WeakReferenceMessenger.Default.Register<PedidoActualizadoMessage>(this, (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => CargarCommand.Execute(null)));
    }

    [ObservableProperty] private bool _isCargandoMas;
    [ObservableProperty] private bool _hayMas;

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

    // Referencia fija — nunca cambia. El workaround null+reassign en PedidosPage.OnAppearing
    // garantiza que BindableLayout tenga siempre UNA SOLA suscripción a CollectionChanged.
    public ObservableCollection<PedidoDto> Pedidos { get; } = new();

    public bool SinPedidos => Pedidos.Count == 0;

    [RelayCommand]
    public async Task CargarAsync()
    {
        _todos.Clear();
        Pedidos.Clear();
        OnPropertyChanged(nameof(SinPedidos));
        _paginaActual = 1;
        try
        {
            var pedidos = await _api.GetMisPedidosAsync(page: 1, pageSize: PageSize);
            _todos.AddRange(pedidos);
            _hayMasServidor = pedidos.Count == PageSize;
        }
        catch { /* ignorar errores de red — AplicarFiltro muestra lista vacía */ }
        AplicarFiltro();
    }

    [RelayCommand]
    private async Task CargarMasAsync()
    {
        if (IsCargandoMas || !HayMas) return;
        IsCargandoMas = true;
        try
        {
            _paginaActual++;
            var pedidos = await _api.GetMisPedidosAsync(page: _paginaActual, pageSize: PageSize);
            _todos.AddRange(pedidos);
            _hayMasServidor = pedidos.Count == PageSize;
            AplicarFiltro();
        }
        catch { /* ignorar errores de red */ }
        finally { IsCargandoMas = false; }
    }

    private void AplicarFiltro()
    {
        var hoy = DateTime.Now.Date;
        Pedidos.Clear();
        var query = FiltroFecha == "Hoy"
            ? _todos.Where(p => p.FechaCreacion.ToLocalTime().Date == hoy)
            : _todos.AsEnumerable();
        foreach (var p in query)
            Pedidos.Add(p);
        HayMas = FiltroFecha != "Hoy" && _hayMasServidor;
        OnPropertyChanged(nameof(SinPedidos));
    }

    [RelayCommand]
    private async Task VerDetallePedidoAsync(PedidoDto pedido)
    {
        await Shell.Current.GoToAsync($"DetallePedido?pedidoId={pedido.Id}");
    }

    /// <summary>FIX-11: Limpia suscripciones de mensajes para evitar memory leaks.</summary>
    public void Cleanup() => WeakReferenceMessenger.Default.UnregisterAll(this);

    public void LimpiarPedidos()
    {
        _todos.Clear();
        Pedidos.Clear();
        HayMas = false;
        _hayMasServidor = false;
        _paginaActual = 1;
        OnPropertyChanged(nameof(SinPedidos));
    }

    /// <summary>BUG-4: Restaura suscripciones al volver a la página (tab cacheado).</summary>
    public void Resubscribe()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        WeakReferenceMessenger.Default.Register<PedidoActualizadoMessage>(this, (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => CargarCommand.Execute(null)));
    }
}


// ── PerfilViewModel ───────────────────────────────────────────────────────────

public partial class PerfilViewModel : ObservableObject
{
    private readonly ApiService              _api;
    private readonly TokenService            _tokens;
    private readonly PushNotificationService _push;
    private readonly CarritoViewModel        _carrito;

    public PerfilViewModel(ApiService api, TokenService tokens, PushNotificationService push, CarritoViewModel carrito)
    {
        _api     = api;
        _tokens  = tokens;
        _push    = push;
        _carrito = carrito;
    }

    [ObservableProperty] private string _nombreCompleto  = string.Empty;
    [ObservableProperty] private string _email           = string.Empty;
    [ObservableProperty] private string _rolTexto        = string.Empty;
    [ObservableProperty] private string _turnoTexto      = string.Empty;
    [ObservableProperty] private bool   _tieneTurno;
    [ObservableProperty] private bool   _esAdmin;
    [ObservableProperty] private string _resumenHorario  = string.Empty;
    [ObservableProperty] private int    _totalPedidos;
    [ObservableProperty] private decimal _totalGastado;

    // Cambio de contraseña (#6)
    [ObservableProperty] private bool   _esEmpleadoCafeteria;
    [ObservableProperty] private bool   _mostrarCambioPassword;
    [ObservableProperty] private string _passwordActual = string.Empty;
    [ObservableProperty] private string _nuevaPassword  = string.Empty;
    [ObservableProperty] private string _confirmarPassword = string.Empty;
    [ObservableProperty] private string _passwordMessage = string.Empty;
    [ObservableProperty] private bool   _passwordIsError;

    [RelayCommand]
    public async Task CargarAsync()
    {
        var usuario = await _tokens.GetUsuarioAsync();
        if (usuario is not null)
        {
            NombreCompleto = usuario.NombreCompleto;
            Email          = usuario.Email;
            RolTexto = usuario.Rol switch
            {
                RolUsuario.Alumno   => "🎓 Alumno",
                RolUsuario.Profesor => "👨‍🏫 Profesor",
                RolUsuario.Personal => "🏢 Personal",
                RolUsuario.Admin     => "⚙️ Admin",
                RolUsuario.Empleado  => "☕ Empleado",
                _                   => "Usuario"
            };
            TieneTurno           = usuario.Turno.HasValue;
            TurnoTexto = usuario.Turno switch
            {
                Turno.Manana => "☀️ Mañana",
                Turno.Tarde  => "🌤️ Tarde",
                Turno.Noche  => "🌙 Noche",
                _            => string.Empty
            };
            EsAdmin              = usuario.Rol == RolUsuario.Admin;
            EsEmpleadoCafeteria  = usuario.Rol == RolUsuario.Empleado;
        }

        // Horario y estadísticas solo son relevantes para usuarios que hacen pedidos
        // PERF-019: llamadas en paralelo
        if (!EsEmpleadoCafeteria)
        {
            var tHorario = _api.GetHorarioStatusAsync();
            var tStats   = _api.GetMisEstadisticasAsync();
            await Task.WhenAll(tHorario, tStats);
            ResumenHorario = tHorario.Result?.Mensaje ?? "Sin información";
            TotalPedidos   = tStats.Result?.TotalPedidos ?? 0;
            TotalGastado   = tStats.Result?.TotalGastado ?? 0;
        }
    }

    [RelayCommand]
    private void ToggleCambioPassword()
    {
        MostrarCambioPassword = !MostrarCambioPassword;
        PasswordMessage  = string.Empty;
        PasswordIsError  = false;
        PasswordActual   = string.Empty;
        NuevaPassword    = string.Empty;
        ConfirmarPassword = string.Empty;
    }

    [RelayCommand]
    private async Task CambiarPasswordAsync()
    {
        PasswordMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(PasswordActual) || string.IsNullOrWhiteSpace(NuevaPassword))
        {
            PasswordMessage = "Rellena todos los campos.";
            PasswordIsError = true;
            return;
        }

        // FIX-15: Validación de complejidad antes de llamar a la API
        if (NuevaPassword.Length < 8 ||
            !NuevaPassword.Any(char.IsUpper) ||
            !NuevaPassword.Any(char.IsDigit) ||
            !NuevaPassword.Any(c => !char.IsLetterOrDigit(c)))
        {
            PasswordMessage = "La contraseña debe tener al menos 8 caracteres, una mayúscula, un número y un símbolo.";
            PasswordIsError = true;
            return;
        }

        if (NuevaPassword != ConfirmarPassword)
        {
            PasswordMessage = "Las contraseñas no coinciden.";
            PasswordIsError = true;
            return;
        }

        var ok = await _api.CambiarPasswordAsync(
            new CambiarPasswordRequest(PasswordActual, NuevaPassword));

        if (ok)
        {
            PasswordActual    = string.Empty;
            NuevaPassword     = string.Empty;
            ConfirmarPassword = string.Empty;
            MostrarCambioPassword = false;
            PasswordMessage  = string.Empty;
            PasswordIsError  = false;

            // D-4: avisar antes de cerrar sesión para que no sea inesperado
            await Shell.Current.DisplayAlert(
                "Contraseña actualizada",
                "Tu contraseña se ha actualizado. Por seguridad, debes iniciar sesión de nuevo.",
                "OK");
            await _api.CerrarSesionAsync();
        }
        else
        {
            PasswordMessage = "La contraseña actual no es correcta.";
            PasswordIsError = true;
        }
    }

    [RelayCommand]
    private async Task CerrarSesionAsync()
    {
        var confirmar = await Shell.Current.DisplayAlert(
            "Cerrar sesión", "¿Seguro que quieres salir?", "Sí", "Cancelar");

        if (!confirmar) return;

        // Eliminar token push antes de borrar credenciales
        await _push.EliminarAsync();
        await _api.DesconectarSignalRAsync();
        _tokens.LimpiarTokens();
        _carrito.LimpiarCarrito();
        await Shell.Current.GoToAsync("//Login");
    }

    [RelayCommand]
    private async Task IrHorariosAsync() =>
        await Shell.Current.GoToAsync("AdminHorarios");

    [RelayCommand]
    private async Task IrInvitacionesAsync() =>
        await Shell.Current.GoToAsync("AdminInvitaciones");

    [RelayCommand]
    private async Task IrIngredientesAsync() =>
        await Shell.Current.GoToAsync("AdminIngredientes");
    }
