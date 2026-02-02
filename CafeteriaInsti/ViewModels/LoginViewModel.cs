using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeteriaInsti.Services;
using CafeteriaInsti.Views;

namespace CafeteriaInsti.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly UserService _userService;
        private readonly SessionService _sessionService;

        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private bool isBusy;

        public LoginViewModel(UserService userService, SessionService sessionService)
        {
            _userService = userService;
            _sessionService = sessionService;
        }

        [RelayCommand]
        private async Task Login()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Por favor ingresa correo y contraseña", "OK");
                return;
            }

            try
            {
                IsBusy = true;

                // Simular espera de red
                await Task.Delay(1000);

                // Validar credenciales contra el archivo
                var (success, message) = _userService.ValidateUser(Email, Password);

                if (success)
                {
                    // Guardar sesión
                    var user = _userService.GetUser(Email);
                    _sessionService.SetSession(Email, user);

                    // Login exitoso - Mostrar la interfaz principal
                    if (Shell.Current is AppShell appShell)
                    {
                        await appShell.ShowMainInterface();
                    }

                    // Limpiar campos
                    Email = string.Empty;
                    Password = string.Empty;
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error", message, "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"No se pudo iniciar sesión: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"Error en Login: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task Register()
        {
            // Navegar a la página de registro usando navegación relativa
            try
            {
                await Shell.Current.GoToAsync(nameof(RegisterPage), animate: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al navegar a Register: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ForgotPassword()
        {
            await Application.Current.MainPage.DisplayAlert("Recuperar", "Navegar a Recuperar Contraseña", "OK");
        }
    }
}
