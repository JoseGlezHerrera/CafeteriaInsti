using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class RegistroInvitacionPage : ContentPage
{
    public RegistroInvitacionPage(RegistroInvitacionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RegistroInvitacionViewModel vm)
            await vm.CargarInstitutosAsync();
    }
}
