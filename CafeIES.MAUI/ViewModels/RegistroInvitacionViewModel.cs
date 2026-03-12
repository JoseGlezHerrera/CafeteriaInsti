using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

[QueryProperty(nameof(TokenInvitacion), "token")]
[QueryProperty(nameof(TipoInvitacion),  "tipo")]
public partial class RegistroInvitacionViewModel : ObservableObject
{
    private readonly ApiService _api;

    public RegistroInvitacionViewModel(ApiService api) => _api = api;

    [ObservableProperty] private string _tokenInvitacion = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RolTexto))]
    private string _tipoInvitacion = string.Empty;
    [ObservableProperty] private string _nombre   = string.Empty;
    [ObservableProperty] private string _email    = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _hayError;

    public string RolTexto => TipoInvitacion == "Profesor"
        ? "👨‍🏫 Profesor"
        : "🏢 Personal";

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
    private async Task ActivarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nombre) || string.IsNullOrWhiteSpace(Email) || Password.Length < 8)
        {
            HayError = true;
            ErrorMessage = "Completa todos los campos (contraseña mínimo 8 caracteres).";
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

        // Conectar SignalR y navegar a Home
        _ = _api.ConectarSignalRAsync();
        try
        {
            await Shell.Current.GoToAsync("//Main");
        }
        catch (Exception ex)
        {
            HayError     = true;
            ErrorMessage = $"Error al abrir la aplicación: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task VolverAsync() => await Shell.Current.GoToAsync("..");
}
