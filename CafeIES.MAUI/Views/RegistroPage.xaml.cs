using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class RegistroPage : ContentPage
{
    public RegistroPage(RegistroViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RegistroViewModel vm)
            await vm.CargarInstitutosAsync();
    }
}
