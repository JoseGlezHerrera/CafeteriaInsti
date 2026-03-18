using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class EmpleadoPedidosPage : ContentPage
{
    private EmpleadoPedidosViewModel Vm => (EmpleadoPedidosViewModel)BindingContext;

    public EmpleadoPedidosPage(EmpleadoPedidosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnPrepararClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is PedidoDto p) Vm.PrepararCommand.Execute(p);
    }
    private void OnListoClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is PedidoDto p) Vm.ListoCommand.Execute(p);
    }
    private void OnEntregarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is PedidoDto p) Vm.EntregarCommand.Execute(p);
    }
    private void OnCancelarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is PedidoDto p) Vm.CancelarCommand.Execute(p);
    }
}
