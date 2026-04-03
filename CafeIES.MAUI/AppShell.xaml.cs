using CafeIES.MAUI.Views;

namespace CafeIES.MAUI;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Tab bar theme — Shell properties don't support DynamicResource in XAML
        ApplyTabBarTheme(Application.Current?.RequestedTheme ?? AppTheme.Unspecified);
        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += (_, e) => ApplyTabBarTheme(e.RequestedTheme);

        // Rutas para navegación programática (push navigation)
        Routing.RegisterRoute("Registro",              typeof(RegistroPage));

        Routing.RegisterRoute("RegistroInvitacion",    typeof(RegistroInvitacionPage));
        Routing.RegisterRoute("ConfirmacionPedido",    typeof(ConfirmacionPedidoPage));
        Routing.RegisterRoute("DetallePedido",         typeof(DetallePedidoPage));
        Routing.RegisterRoute("AdminEditProducto",     typeof(AdminEditProductoPage));
        Routing.RegisterRoute("ProductoDetalle",       typeof(ProductoDetallePage));
        Routing.RegisterRoute("AdminHorarios",         typeof(AdminHorariosPage));
        Routing.RegisterRoute("AdminInvitaciones",     typeof(AdminInvitacionesPage));
        Routing.RegisterRoute("AdminIngredientes",     typeof(AdminIngredientesPage));
        Routing.RegisterRoute("PagamentoWeb",          typeof(PagamentoWebPage));
    }

    private void ApplyTabBarTheme(AppTheme theme)
    {
        bool isDark = theme == AppTheme.Dark;
        Shell.SetTabBarBackgroundColor(this,
            isDark ? Color.FromArgb("#1A1916") : Color.FromArgb("#FFFFFF"));
        Shell.SetTabBarUnselectedColor(this,
            isDark ? Color.FromArgb("#7A7468") : Color.FromArgb("#9E978F"));
    }
}
