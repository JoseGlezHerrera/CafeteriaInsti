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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Vm.Resubscribe();
    }

    // FIX-11: Desuscribir mensajes al desaparecer la página
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Vm.Cleanup();
    }

    private async void OnPrepararClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        await AnimatePress(btn);
        if (btn.CommandParameter is PedidoDto p) Vm.PrepararCommand.Execute(p);
    }

    private async void OnListoClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        await AnimatePress(btn);
        if (btn.CommandParameter is PedidoDto p) Vm.ListoCommand.Execute(p);
    }

    private async void OnEntregarClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        await AnimatePress(btn);
        if (btn.CommandParameter is PedidoDto p) Vm.EntregarCommand.Execute(p);
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        await AnimatePress(btn);
        if (btn.CommandParameter is PedidoDto p) Vm.CancelarCommand.Execute(p);
    }

    private static async Task AnimatePress(VisualElement el)
    {
        await el.ScaleTo(0.88, 80, Easing.CubicIn);
        await el.ScaleTo(1.0,  80, Easing.CubicOut);
    }
}
