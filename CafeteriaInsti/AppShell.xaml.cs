using CafeteriaInsti.Views;

namespace CafeteriaInsti
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // ✅ REGISTRAR RUTAS PARA NAVEGACIÓN
            Routing.RegisterRoute(nameof(ListaProductosPage), typeof(ListaProductosPage));
            Routing.RegisterRoute(nameof(FavoritosPage), typeof(FavoritosPage));
            Routing.RegisterRoute(nameof(CarritoPage), typeof(CarritoPage));
            Routing.RegisterRoute(nameof(HistorialPedidosPage), typeof(HistorialPedidosPage));
            Routing.RegisterRoute(nameof(InformacionPage), typeof(InformacionPage));
            Routing.RegisterRoute(nameof(DetalleProductoPage), typeof(DetalleProductoPage));
            Routing.RegisterRoute(nameof(ConfirmacionPedidoPage), typeof(ConfirmacionPedidoPage));
        }
    }
}


