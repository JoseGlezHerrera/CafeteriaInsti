using CafeIES.MAUI.Views;

namespace CafeIES.MAUI;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Rutas para navegación programática (push navigation)
        Routing.RegisterRoute("Registro",              typeof(RegistroPage));
        Routing.RegisterRoute("RegistroInvitacion",    typeof(RegistroInvitacionPage));
        Routing.RegisterRoute("ConfirmacionPedido",    typeof(ConfirmacionPedidoPage));
        Routing.RegisterRoute("DetallePedido",         typeof(DetallePedidoPage));
        Routing.RegisterRoute("AdminEditProducto",        typeof(AdminEditProductoPage));
    }
}
