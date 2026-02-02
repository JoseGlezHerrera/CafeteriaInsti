using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeteriaInsti.Services;
using CafeteriaInsti.Views;

namespace CafeteriaInsti.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly UserService _userService;

        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private string confirmPassword;

        [ObservableProperty]
        private string fullName;

        [ObservableProperty]
        private string turno;

        [ObservableProperty]
        private bool isBusy;

        public RegisterViewModel(UserService userService)
        {
            _userService = userService;
            // Inicializar con valor por defecto
            Turno = "Mañana";
        }

        [RelayCommand]
        private async Task Register()
        {
            if (IsBusy) return;

            // Validaciones
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || 
                string.IsNullOrWhiteSpace(ConfirmPassword) || string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Turno))
            {
                await Application.Current.MainPage.DisplayAlert("Error", 
                    "Por favor completa todos los campos", "OK");
                return;
            }

            if (Password != ConfirmPassword)
            {
                await Application.Current.MainPage.DisplayAlert("Error", 
                    "Las contraseñas no coinciden", "OK");
                return;
            }

            try
            {
                IsBusy = true;

                // Registrar usuario con turno
                var (success, message) = _userService.RegisterUser(Email, Password, FullName, Turno);

                if (success)
                {
                    await Application.Current.MainPage.DisplayAlert("Éxito", 
                        message, "OK");

                    // Volver a login
                    try
                    {
                        await Shell.Current.GoToAsync("..", animate: false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al navegar de vuelta: {ex.Message}");
                    }

                    // Limpiar campos
                    Email = string.Empty;
                    Password = string.Empty;
                    ConfirmPassword = string.Empty;
                    FullName = string.Empty;
                    Turno = "Mañana";
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error", message, "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", 
                    $"Error al registrarse: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"Error en Register: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GoToLogin()
        {
            // Volver a login usando navegación relativa
            try
            {
                await Shell.Current.GoToAsync("..", animate: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al navegar a Login: {ex.Message}");
            }
        }
    }
}
