using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class RegistroViewModel : ObservableObject
{
    private readonly ApiService _api;

    public RegistroViewModel(ApiService api) => _api = api;

    [ObservableProperty] private string _nombreCompleto     = string.Empty;
    [ObservableProperty] private string _email              = string.Empty;
    [ObservableProperty] private string _password           = string.Empty;
    [ObservableProperty] private string _confirmarPassword  = string.Empty;
    [ObservableProperty] private string _errorMessage       = string.Empty;
    [ObservableProperty] private bool   _hayError;
    [ObservableProperty] private bool   _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TurnoMañanaSeleccionado), nameof(TurnoTardeSeleccionado), nameof(TurnoNocheSeleccionado))]
    private Turno _turnoSeleccionado = Turno.Manana;

    public bool TurnoMañanaSeleccionado => TurnoSeleccionado == Turno.Manana;
    public bool TurnoTardeSeleccionado  => TurnoSeleccionado == Turno.Tarde;
    public bool TurnoNocheSeleccionado  => TurnoSeleccionado == Turno.Noche;

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
    private void SeleccionarTurno(string turno)
    {
        TurnoSeleccionado = turno switch
        {
            "Manana" => Turno.Manana,
            "Tarde"  => Turno.Tarde,
            "Noche"  => Turno.Noche,
            _        => Turno.Manana
        };
    }

    [RelayCommand]
    private async Task RegistrarAsync()
    {
        if (string.IsNullOrWhiteSpace(NombreCompleto) ||
            string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(Password))
        {
            HayError     = true;
            ErrorMessage = "Por favor rellena todos los campos.";
            return;
        }

        if (!Email.Contains('@') || !Email.Contains('.'))
        {
            HayError     = true;
            ErrorMessage = "Introduce un email válido.";
            return;
        }

        if (Password.Length < 8)
        {
            HayError     = true;
            ErrorMessage = "La contraseña debe tener al menos 8 caracteres.";
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

        var resultado = await _api.RegistroAlumnoAsync(new RegistroAlumnoRequest(
            NombreCompleto, Email, Password, TurnoSeleccionado, InstitutoSeleccionado.Id));

        IsLoading = false;

        if (resultado != RegistroResultado.Ok)
        {
            HayError     = true;
            ErrorMessage = resultado == RegistroResultado.EmailDuplicado
                ? "Ese correo ya tiene una cuenta registrada."
                : "No se pudo conectar con el servidor. Comprueba tu conexión e inténtalo de nuevo.";
            return;
        }

        await Shell.Current.DisplayAlert(
            "¡Registro completado! 🎉",
            "Tu cuenta está pendiente de validación por el administrador. Te avisarán cuando esté activa.",
            "Entendido");

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task VolverAsync() => await Shell.Current.GoToAsync("..");
}
