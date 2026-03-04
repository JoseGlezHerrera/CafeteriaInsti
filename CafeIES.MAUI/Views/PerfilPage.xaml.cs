using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class PerfilPage : ContentPage
{
    public PerfilPage(PerfilViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
