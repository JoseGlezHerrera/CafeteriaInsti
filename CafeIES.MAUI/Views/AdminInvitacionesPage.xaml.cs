using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class AdminInvitacionesPage : ContentPage
{
    public AdminInvitacionesPage(AdminInvitacionesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }


}
