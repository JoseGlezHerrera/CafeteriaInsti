using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class DetallePedidoPage : ContentPage
{
    private readonly DetallePedidoViewModel _vm;

    public DetallePedidoPage(DetallePedidoViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Cleanup();
    }
}
