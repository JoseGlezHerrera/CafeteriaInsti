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
    private readonly ApiService _api;

    public PedidosViewModel(ApiService api)
    {
        _api = api;
        WeakReferenceMessenger.Default.Register<PedidoActualizadoMessage>(this, (_, _) =>
            MainThread.BeginInvokeOnMainThread(async () => await CargarAsync()));
    }

    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<PedidoDto> Pedidos { get; } = new();

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsLoading = true;
        var pedidos = await _api.GetMisPedidosAsync();
        Pedidos.Clear();
        foreach (var p in pedidos) Pedidos.Add(p);
        IsLoading = false;
    }

    [RelayCommand]
    private async Task VerDetallePedidoAsync(PedidoDto pedido)
    {
        await Shell.Current.GoToAsync($"DetallePedido?pedidoId={pedido.Id}");
    }
}


// ── PerfilViewModel ───────────────────────────────────────────────────────────

public partial class PerfilViewModel : ObservableObject
{
    private readonly ApiService   _api;
    private readonly TokenService _tokens;

    public PerfilViewModel(ApiService api, TokenService tokens)
    {
        _api    = api;
        _tokens = tokens;
    }

    [ObservableProperty] private string _nombreCompleto  = string.Empty;
    [ObservableProperty] private string _email           = string.Empty;
    [ObservableProperty] private string _rolTexto        = string.Empty;
    [ObservableProperty] private string _turnoTexto      = string.Empty;
    [ObservableProperty] private bool   _tieneTurno;
    [ObservableProperty] private string _resumenHorario  = string.Empty;
    [ObservableProperty] private int    _totalPedidos;
    [ObservableProperty] private decimal _totalGastado;

    // Cambio de contraseña (#6)
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
                RolUsuario.Admin    => "⚙️ Admin",
                _                  => "Usuario"
            };
            TieneTurno = usuario.Turno.HasValue;
            TurnoTexto = usuario.Turno switch
            {
                Turno.Manana => "☀️ Mañana",
                Turno.Tarde  => "🌤️ Tarde",
                Turno.Noche  => "🌙 Noche",
                _            => string.Empty
            };
        }

        var status = await _api.GetHorarioStatusAsync();
        ResumenHorario = status?.Mensaje ?? "Sin información";

        var stats = await _api.GetMisEstadisticasAsync();
        TotalPedidos = stats?.TotalPedidos ?? 0;
        TotalGastado = stats?.TotalGastado ?? 0;
    }

    [RelayCommand]
    private void ToggleCambioPassword()
    {
        MostrarCambioPassword = !MostrarCambioPassword;
        PasswordMessage = string.Empty;
        PasswordActual = string.Empty;
        NuevaPassword = string.Empty;
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

        if (NuevaPassword.Length < 8)
        {
            PasswordMessage = "La nueva contraseña debe tener al menos 8 caracteres.";
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
            PasswordActual = string.Empty;
            NuevaPassword = string.Empty;
            ConfirmarPassword = string.Empty;
            MostrarCambioPassword = false;
            PasswordMessage = string.Empty;

            var toast = Toast.Make("✓ Contraseña actualizada correctamente", ToastDuration.Short, 14);
            await toast.Show();
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

        await _api.DesconectarSignalRAsync();
        _tokens.LimpiarTokens();
        await Shell.Current.GoToAsync("//Login");
    }

    }
