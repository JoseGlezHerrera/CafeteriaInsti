using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class AdminIngredientesPage : ContentPage
{
    private readonly AdminIngredientesViewModel _vm;

    public AdminIngredientesPage(AdminIngredientesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is IngredienteDto ing)
            await _vm.EditarCommand.ExecuteAsync(ing);
    }

    private async void OnToggleActivoClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is IngredienteDto ing)
            await _vm.ToggleActivoCommand.ExecuteAsync(ing);
    }

    private async void OnEliminarClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is IngredienteDto ing)
            await _vm.EliminarCommand.ExecuteAsync(ing);
    }
}
