// ViewModels/InformacionViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeteriaInsti.ViewModels
{
    public partial class InformacionViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string _nombreCafeteria = "Cafeteria del Instituto";

        [ObservableProperty]
        private string _direccion = "Calle Principal 123, Ciudad";

        [ObservableProperty]
        private string _telefono = "+34 123 456 789";

        [ObservableProperty]
        private string _email = "cafeteria@instituto.edu";

        [ObservableProperty]
        private string _horarioLunesViernes = "08:00 - 17:00";

        [ObservableProperty]
        private string _horarioSabado = "09:00 - 14:00";

        [ObservableProperty]
        private string _horarioDomingo = "Cerrado";

        public InformacionViewModel()
        {
            Title = "Informacion";
        }

        [RelayCommand]
        private async Task LlamarTelefono()
        {
            try
            {
                if (PhoneDialer.Default.IsSupported)
                {
                    PhoneDialer.Default.Open(_telefono);
                }
                else
                {
                    await Shell.Current.DisplayAlert("Info", $"Telefono: {_telefono}", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Llamar telefono: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task EnviarEmail()
        {
            try
            {
                var message = new EmailMessage
                {
                    Subject = "Consulta desde la app",
                    To = new List<string> { _email }
                };
                
                await Microsoft.Maui.ApplicationModel.Communication.Email.Default.ComposeAsync(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Enviar email: {ex.Message}");
                await Shell.Current.DisplayAlert("Info", $"Email: {_email}", "OK");
            }
        }

        [RelayCommand]
        private async Task AbrirMapa()
        {
            try
            {
                // Coordenadas de ejemplo - Reemplazar con las reales
                var location = new Location(40.4168, -3.7038); // Madrid ejemplo
                var options = new MapLaunchOptions { Name = _nombreCafeteria };

                await Map.Default.OpenAsync(location, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Abrir mapa: {ex.Message}");
                await Shell.Current.DisplayAlert("Info", $"Direccion: {_direccion}", "OK");
            }
        }
    }
}
