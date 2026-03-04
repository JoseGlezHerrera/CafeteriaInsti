using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class DetallePedidoPage : ContentPage
{
    public DetallePedidoPage(DetallePedidoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
