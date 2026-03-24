using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class PedidosPage : ContentPage
{
    private readonly PedidosViewModel _vm;

    public PedidosPage(PedidosViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }


}
