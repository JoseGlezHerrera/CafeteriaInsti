using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class AdminIngredientesPage : ContentPage
{
    public AdminIngredientesPage(AdminIngredientesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
