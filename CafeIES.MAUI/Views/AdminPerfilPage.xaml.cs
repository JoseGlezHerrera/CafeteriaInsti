using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class AdminPerfilPage : ContentPage
{
    public AdminPerfilPage(PerfilViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
