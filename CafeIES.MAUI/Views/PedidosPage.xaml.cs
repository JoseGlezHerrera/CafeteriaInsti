using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class PedidosPage : ContentPage
{
    private readonly PedidosViewModel _vm;

    public PedidosPage(PedidosViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Recargar siempre al mostrar la página para evitar ver datos de otra sesión
        _ = _vm.CargarAsync();
    }
}
