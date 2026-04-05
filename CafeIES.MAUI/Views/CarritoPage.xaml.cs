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

        // Workaround para bug de MAUI: BindableLayout puede duplicar visualmente los items
        // cuando se navega a la página desde otra tab. Reasignar null + colección fuerza
        // un repintado limpio sin afectar los datos del ObservableCollection.
        BindableLayout.SetItemsSource(ItemsList, null);
        BindableLayout.SetItemsSource(ItemsList, Vm.Items);

        // Recargar estado de desayuno cada vez que se abre el carrito
        // para reflejar si ya se consumió el desayuno en un pedido anterior del día
        _ = Vm.CargarDesayunoStatusAsync();
    }

    private async void BtnPagar_Pressed(object? sender, EventArgs e)
        => await BtnPagar.ScaleTo(0.94, 80, Easing.CubicIn);

    private async void BtnPagar_Released(object? sender, EventArgs e)
        => await BtnPagar.ScaleTo(1.0, 80, Easing.CubicOut);
}
