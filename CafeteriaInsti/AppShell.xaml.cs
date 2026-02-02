using CafeteriaInsti.Views;

namespace CafeteriaInsti
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // ✅ REGISTRAR RUTAS PARA NAVEGACIÓN
            // LoginPage y RegisterPage están en la misma jerarquía
            Routing.RegisterRoute("LoginPage/RegisterPage", typeof(RegisterPage));

            // Rutas para la interfaz principal
            Routing.RegisterRoute("ListaProductosPage/EditProfilePage", typeof(EditProfilePage));
            Routing.RegisterRoute("ListaProductosPage/DetalleProductoPage", typeof(DetalleProductoPage));
            Routing.RegisterRoute("ListaProductosPage/CarritoPage", typeof(CarritoPage));
            Routing.RegisterRoute("CarritoPage/ConfirmacionPedidoPage", typeof(ConfirmacionPedidoPage));
        }

        /// <summary>
        /// Llamar después de login exitoso para mostrar la interfaz principal
        /// </summary>
        public async Task ShowMainInterface()
        {
            try
            {
                var mainTabBar = this.FindByName<TabBar>("MainTabBar");
                if (mainTabBar != null)
                {
                    mainTabBar.IsVisible = true;
                }

                // Navegar a la lista de productos
                await Shell.Current.GoToAsync($"//{nameof(ListaProductosPage)}", animate: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ShowMainInterface: {ex.Message}");
            }
        }

        /// <summary>
        /// Llamar al logout para volver al login
        /// </summary>
        public async Task ShowLoginPage()
        {
            try
            {
                var mainTabBar = this.FindByName<TabBar>("MainTabBar");
                if (mainTabBar != null)
                {
                    mainTabBar.IsVisible = false;
                }

                // Volver a LoginPage
                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}", animate: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ShowLoginPage: {ex.Message}");
            }
        }
    }
}

