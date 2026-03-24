using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class PerfilPage : ContentPage
{
    public PerfilPage(PerfilViewModel vm)
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
