using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.MAUI.Services;
using CafeIES.Shared.Models;
using Microsoft.Extensions.Logging;

namespace CafeIES.MAUI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ApiService              _api;
    private readonly PushNotificationService _push;
    private readonly TokenService            _tokens;
    private readonly ILogger<LoginViewModel> _logger;

    public LoginViewModel(ApiService api, PushNotificationService push, TokenService tokens, ILogger<LoginViewModel> logger)
    {
        _api    = api;
        _push   = push;
        _tokens = tokens;
        _logger = logger;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hayError;

    private bool PuedeLogin =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !IsLoading;

    [RelayCommand(CanExecute = nameof(PuedeLogin))]
    private async Task LoginAsync()
    {
        IsLoading    = true;
        HayError     = false;
        ErrorMessage = string.Empty;

        var (resultado, motivo) = await _api.LoginAsync(Email, Password);

        IsLoading = false;

        if (resultado is null)
        {
            HayError     = true;
            ErrorMessage = motivo switch
            {
                MotivoRechazo.Pendiente  => "Tu cuenta está pendiente de validación por el administrador.",
                MotivoRechazo.Suspendida => "Tu cuenta ha sido suspendida. Contacta con administración.",
                MotivoRechazo.Rechazada  => "Tu solicitud de registro fue rechazada.",
                _                        => "Email o contraseña incorrectos."
            };
            return;
        }

        // Conectar SignalR y registrar token push en segundo plano
        _ = _api.ConectarSignalRAsync();
        _ = _push.RegistrarAsync();

        // Navegar al TabBar según el rol
        var destino = resultado.Usuario.Rol switch
        {
            RolUsuario.Admin    => "//Admin",
            RolUsuario.Empleado => "//Empleado",
            _                   => "//Main"
        };
        try
        {
            await Shell.Current.GoToAsync(destino);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de navegación al destino {Destino} tras el login.", destino);
            HayError     = true;
            ErrorMessage = $"Error al abrir la aplicación: {ex.Message}";
        }
    }

    public async Task TryAutoLoginAsync()
    {
        var usuario = await _tokens.GetUsuarioAsync();
        if (usuario is null) return;

        IsLoading = true;
        try
        {
            _ = _api.ConectarSignalRAsync();
            _ = _push.RegistrarAsync();
            var destino = usuario.Rol switch
            {
                RolUsuario.Admin    => "//Admin",
                RolUsuario.Empleado => "//Empleado",
                _                   => "//Main"
            };
            await Shell.Current.GoToAsync(destino);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-login fallido.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task IrARegistroAsync()
    {
        await Shell.Current.GoToAsync("Registro");
    }

    [RelayCommand]
    private async Task IrARegistroInvitacionAsync()
    {
        await Shell.Current.GoToAsync("RegistroInvitacion");
    }


}
