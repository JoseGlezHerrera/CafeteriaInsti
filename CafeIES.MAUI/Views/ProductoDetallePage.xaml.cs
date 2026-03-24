using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class ProductoDetallePage : ContentPage
{
    public ProductoDetallePage(ProductoDetalleViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = ((ProductoDetalleViewModel)BindingContext).CargarAsync();
    }
}
