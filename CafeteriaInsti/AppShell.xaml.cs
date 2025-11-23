using CafeteriaInsti.Views;

namespace CafeteriaInsti
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // ✅ REGISTRAR LA RUTA
            Routing.RegisterRoute(nameof(DetalleProductoPage), typeof(DetalleProductoPage));
        }
    }
}
