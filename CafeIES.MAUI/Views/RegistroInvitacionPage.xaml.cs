using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class RegistroInvitacionPage : ContentPage
{
    public RegistroInvitacionPage(RegistroInvitacionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
