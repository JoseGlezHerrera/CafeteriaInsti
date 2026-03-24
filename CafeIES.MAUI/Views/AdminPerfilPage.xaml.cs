using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class AdminPerfilPage : ContentPage
{
    public AdminPerfilPage(PerfilViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = ((PerfilViewModel)BindingContext).CargarAsync();
    }
}
