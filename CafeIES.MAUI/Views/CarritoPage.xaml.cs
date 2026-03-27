using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class CarritoPage : ContentPage
{
    private CarritoViewModel Vm => (CarritoViewModel)BindingContext;

    public CarritoPage(CarritoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Recargar estado de desayuno cada vez que se abre el carrito
        // para reflejar si ya se consumió el desayuno en un pedido anterior del día
        _ = Vm.CargarDesayunoStatusAsync();
    }
}
