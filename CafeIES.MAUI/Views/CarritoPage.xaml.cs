using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class CarritoPage : ContentPage
{
    public CarritoPage(CarritoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
