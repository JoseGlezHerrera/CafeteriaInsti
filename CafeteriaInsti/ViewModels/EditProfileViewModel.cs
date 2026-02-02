using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeteriaInsti.Services;
using CafeteriaInsti.Views;

namespace CafeteriaInsti.ViewModels
{
    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly UserService _userService;
        private readonly SessionService _sessionService;
        private string _originalEmail;

        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string fullName;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private string confirmPassword;

        [ObservableProperty]
        private string turno;

        [ObservableProperty]
        private bool isBusy;

        public EditProfileViewModel(UserService userService, SessionService sessionService)
        {
            _userService = userService;
            _sessionService = sessionService;

            // Cargar datos actuales del usuario
            if (_sessionService.IsLoggedIn && _sessionService.CurrentUser != null)
            {
                _originalEmail = _sessionService.CurrentUser.Email;
                this.email = _sessionService.CurrentUser.Email;
                this.fullName = _sessionService.CurrentUser.FullName;
                this.password = _sessionService.CurrentUser.Password;
                this.confirmPassword = _sessionService.CurrentUser.Password;
                this.turno = _sessionService.CurrentUser.Turno ?? "Mañana";
            }
        }

        [RelayCommand]
        private async Task SaveChanges()
        {
            if (this.isBusy) return;

            // Validaciones
            if (string.IsNullOrWhiteSpace(this.email) || string.IsNullOrWhiteSpace(this.fullName) || 
                string.IsNullOrWhiteSpace(this.password) || string.IsNullOrWhiteSpace(this.confirmPassword) ||
                string.IsNullOrWhiteSpace(this.turno))
            {
                await Application.Current.MainPage.DisplayAlert("Error", 
                    "Por favor completa todos los campos", "OK");
                return;
            }

            if (this.password != this.confirmPassword)
            {
                await Application.Current.MainPage.DisplayAlert("Error", 
                    "Las contraseñas no coinciden", "OK");
                return;
            }

            try
            {
                this.isBusy = true;

                System.Diagnostics.Debug.WriteLine($"[DEBUG] Guardando cambios - Email actual: {_originalEmail}, Nuevo email: {this.email}");

                // Actualizar usuario
                var (success, message) = _userService.UpdateUser(
                    _originalEmail, this.email, this.fullName, this.password, this.turno);

                System.Diagnostics.Debug.WriteLine($"[DEBUG] Resultado de UpdateUser: {success}, {message}");

                if (success)
                {
                    await Application.Current.MainPage.DisplayAlert("Éxito", message, "OK");

                    // Actualizar sesión con nuevos datos
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Obteniendo usuario actualizado con email: {this.email}");
                    var updatedUser = _userService.GetUser(this.email);

                    if (updatedUser != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Usuario encontrado - Nombre: {updatedUser.FullName}, Email: {updatedUser.Email}");
                        _sessionService.SetSession(this.email, updatedUser);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Sesión actualizada correctamente");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ERROR] No se encontró el usuario con email: {this.email}");
                        await Application.Current.MainPage.DisplayAlert("Advertencia", 
                            "Los cambios se guardaron pero no se pudo actualizar la sesión. Por favor, reinicia la sesión.", "OK");
                    }

                    // Volver a lista de productos
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Navegando hacia atrás");
                        await Shell.Current.GoToAsync("..", animate: false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al navegar: {ex.Message}");
                    }
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error", message, "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", 
                    $"Error al guardar cambios: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"Error en SaveChanges: {ex}");
            }
            finally
            {
                this.isBusy = false;
            }
        }

        [RelayCommand]
        private async Task Cancel()
        {
            try
            {
                await Shell.Current.GoToAsync("..", animate: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al volver: {ex.Message}");
            }
        }
    }
}
