using Microsoft.Extensions.DependencyInjection;

namespace CafeteriaInsti
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Obtener el servicio AppShell del contenedor de inyección de dependencias
            var appShell = IPlatformApplication.Current?.Services.GetService<AppShell>();
            if (appShell == null)
            {
                appShell = new AppShell();
            }
            return new Window(appShell);
        }
    }
}

