using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class AdminHorariosPage : ContentPage
{
    public AdminHorariosPage(AdminHorariosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }


}
