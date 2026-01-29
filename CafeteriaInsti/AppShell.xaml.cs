using CafeteriaInsti.Views;

namespace CafeteriaInsti
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // ✅ REGISTRAR RUTAS PARA NAVEGACIÓN
            Routing.RegisterRoute(nameof(DetalleProductoPage), typeof(DetalleProductoPage));
            Routing.RegisterRoute(nameof(ConfirmacionPedidoPage), typeof(ConfirmacionPedidoPage));
        }
    }
}

