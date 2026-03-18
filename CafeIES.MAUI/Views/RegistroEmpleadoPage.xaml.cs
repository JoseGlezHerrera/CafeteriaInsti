using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class RegistroEmpleadoPage : ContentPage
{
    public RegistroEmpleadoPage(RegistroEmpleadoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
