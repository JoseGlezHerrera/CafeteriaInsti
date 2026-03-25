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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Resubscribe();
    }

    // FIX-11: Desuscribir mensajes al desaparecer la página
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Cleanup();
    }
}
