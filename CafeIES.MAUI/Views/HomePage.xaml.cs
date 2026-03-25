using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Resubscribe();
    }

    // FIX-12: Desuscribir al desaparecer para evitar memory leaks
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Cleanup();
    }
}
