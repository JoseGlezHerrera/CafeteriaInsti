using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class PerfilPage : ContentPage
{
    private readonly PerfilViewModel _vm;

    public PerfilPage(PerfilViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // BUG-018: sin OnAppearing los datos del perfil nunca se cargaban
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.CargarCommand.Execute(null);
    }
}
