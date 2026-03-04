using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class AdminProductosPage : ContentPage
{
    private AdminProductosViewModel Vm => (AdminProductosViewModel)BindingContext;

    public AdminProductosPage(AdminProductosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnEditarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is ProductoDto p)
            Vm.EditarCommand.Execute(p);
    }

    private void OnToggleActivoClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is ProductoDto p)
            Vm.ToggleActivoCommand.Execute(p);
    }

    private void OnEliminarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is ProductoDto p)
            Vm.EliminarCommand.Execute(p);
    }
}
