using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

[QueryProperty(nameof(TokenInvitacion), "token")]
[QueryProperty(nameof(TipoInvitacion),  "tipo")]
public partial class RegistroInvitacionViewModel : ObservableObject
{
    private readonly ApiService              _api;
    private readonly PushNotificationService _push;
    private readonly ILogger<RegistroInvitacionViewModel> _logger;

    public RegistroInvitacionViewModel(ApiService api, PushNotificationService push,
        ILogger<RegistroInvitacionViewModel> logger)
    {
        _api    = api;
        _push   = push;
        _logger = logger;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TokenVerificado))]
    private string _tokenInvitacion = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RolTexto))]
    [NotifyPropertyChangedFor(nameof(TokenVerificado))]
    private string _tipoInvitacion = string.Empty;

    // Código que el usuario escribe manualmente (si llega sin token via query param)
    [ObservableProperty] private string _codigoManual = string.Empty;

    [ObservableProperty] private string _nombre             = string.Empty;
    [ObservableProperty] private string _email              = string.Empty;
    [ObservableProperty] private string _password           = string.Empty;
    [ObservableProperty] private string _confirmarPassword  = string.Empty;
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _hayError;

    // true cuando tenemos token + tipo validados → muestra el formulario de registro
    public bool TokenVerificado => !string.IsNullOrEmpty(TokenInvitacion) && !string.IsNullOrEmpty(TipoInvitacion);

    public string RolTexto => TipoInvitacion switch
    {
        "Profesor" => "👨‍🏫 Profesor",
        "Empleado" => "☕ Empleado de cafetería",
        _          => "🏢 Personal"
    };

    // ── Instituto ─────────────────────────────────────────────────────────────
    public ObservableCollection<InstitutoDto> Institutos { get; } = new();

    [ObservableProperty] private InstitutoDto? _institutoSeleccionado;

    [RelayCommand]
    public async Task CargarInstitutosAsync()
    {
        if (Institutos.Count > 0) return;
        var lista = await _api.GetInstitutosAsync();
        Institutos.Clear();
        foreach (var i in lista) Institutos.Add(i);
        if (Institutos.Count == 1) InstitutoSeleccionado = Institutos[0];
    }

    [RelayCommand]
    private async Task VerificarCodigoAsync()
    {
        if (string.IsNullOrWhiteSpace(CodigoManual))
        {
            HayError     = true;
            ErrorMessage = "Introduce el código de invitación.";
            return;
        }

        IsLoading = true;
        HayError  = false;

        var (valida, tipo, token) = await _api.ValidarInvitacionAsync(CodigoManual.Trim());

        IsLoading = false;

        if (!valida)
        {
            HayError     = true;
            ErrorMessage = "El código no es válido o ha expirado. Solicita uno nuevo al administrador.";
            return;
        }

        TokenInvitacion = token;
        TipoInvitacion  = tipo;
        await CargarInstitutosAsync();
    }

    [RelayCommand]
    private async Task ActivarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nombre) || string.IsNullOrWhiteSpace(Email) || Password.Length < 8)
        {
            HayError     = true;
            ErrorMessage = "Completa todos los campos (contraseña mínimo 8 caracteres).";
            return;
        }

        if (Password != ConfirmarPassword)
        {
            HayError     = true;
            ErrorMessage = "Las contraseñas no coinciden. Compruébalas e inténtalo de nuevo.";
            return;
        }

        if (InstitutoSeleccionado is null)
        {
            HayError     = true;
            ErrorMessage = "Selecciona tu instituto.";
            return;
        }

        IsLoading = true;
        HayError  = false;

        var req = new RegistroInvitadoRequest(TokenInvitacion, Nombre, Email, Password, InstitutoSeleccionado.Id);
        var resultado = await _api.RegistroInvitadoAsync(req);

        IsLoading = false;

        if (resultado is null)
        {
            HayError     = true;
            ErrorMessage = "El enlace ha expirado o ya no es válido. Solicita uno nuevo al administrador.";
            return;
        }

        // Conectar SignalR y registrar token push en segundo plano
        _ = _api.ConectarSignalRAsync();
        _ = _push.RegistrarAsync();
        try
        {
            await Shell.Current.GoToAsync("//Main");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de navegación a //Main tras el registro por invitación.");
            HayError     = true;
            ErrorMessage = $"Error al abrir la aplicación: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task VolverAsync() => await Shell.Current.GoToAsync("..");
}
