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
        var navego = await _vm.TryAutoLoginAsync();
        // Solo mostrar el formulario si no hubo auto-login (evita FadeTo sobre página desenganchada)
        if (!navego)
            await ContenidoLogin.FadeTo(1, 180);
    }
}
