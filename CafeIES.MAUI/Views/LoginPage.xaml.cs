using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
