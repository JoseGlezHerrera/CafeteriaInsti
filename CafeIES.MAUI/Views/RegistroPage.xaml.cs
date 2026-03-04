using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class RegistroPage : ContentPage
{
    public RegistroPage(RegistroViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
