using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class AdminEditProductoPage : ContentPage
{
    public AdminEditProductoPage(AdminEditProductoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
