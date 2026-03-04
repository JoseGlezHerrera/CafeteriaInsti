using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class PedidosPage : ContentPage
{
    public PedidosPage(PedidosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
