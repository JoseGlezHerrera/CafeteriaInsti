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
        PageContent.Opacity      = 0;
        PageContent.TranslationY = 18;
        PageContent.FadeTo(1, 280, Easing.CubicOut);
        PageContent.TranslateTo(0, 0, 280, Easing.CubicOut);
    }
}
