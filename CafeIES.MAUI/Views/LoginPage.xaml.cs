using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.TryAutoLoginAsync();
        // Si seguimos aquí, no hay sesión guardada — mostrar el formulario
        await ContenidoLogin.FadeTo(1, 180);
    }
}
