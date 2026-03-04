using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.MAUI.Services;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ApiService _api;

    public LoginViewModel(ApiService api)
    {
        _api = api;
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

        var (resultado, pendiente) = await _api.LoginAsync(Email, Password);

        IsLoading = false;

        if (resultado is null)
        {
            HayError     = true;
            ErrorMessage = pendiente
                ? "Tu cuenta está pendiente de validación por el administrador."
                : "Usuario o contraseña incorrectos.";
            return;
        }

        // Navegar al TabBar según el rol
        var destino = resultado.Usuario.Rol == RolUsuario.Admin ? "//Admin" : "//Main";
        try
        {
            await Shell.Current.GoToAsync(destino);
        }
        catch (Exception ex)
        {
            HayError     = true;
            ErrorMessage = $"Error al abrir la aplicación: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task IrARegistroAsync()
    {
        await Shell.Current.GoToAsync("Registro");
    }
}
