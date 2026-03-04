using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
