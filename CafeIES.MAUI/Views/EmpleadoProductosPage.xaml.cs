using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class EmpleadoProductosPage : ContentPage
{
    private EmpleadoProductosViewModel Vm => (EmpleadoProductosViewModel)BindingContext;

    public EmpleadoProductosPage(EmpleadoProductosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = Vm.CargarAsync();
    }

    private void OnEditarStockClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is ProductoDto p) Vm.EditarStockCommand.Execute(p);
    }

    private void OnToggleActivoClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is ProductoDto p) Vm.ToggleActivoCommand.Execute(p);
    }
}
