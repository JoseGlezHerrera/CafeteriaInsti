using CafeIES.MAUI.Controls;
using CafeIES.MAUI.Services;
using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class EmpleadoPedidosPage : ContentPage
{
    private EmpleadoPedidosViewModel Vm => (EmpleadoPedidosViewModel)BindingContext;
    private readonly IPrintService _print;

    public EmpleadoPedidosPage(EmpleadoPedidosViewModel vm, IPrintService print)
    {
        InitializeComponent();
        BindingContext = vm;
        _print = print;
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

    private void OnImprimirRequested(object? sender, PedidoDto p) =>
        _ = _print.ImprimirAsync(TicketHtmlBuilder.Build(p), $"Pedido #{p.NumeroPedido:D3}");
    private void OnPrepararRequested(object? sender, PedidoDto p)  => Vm.PrepararCommand.Execute(p);
    private void OnListoRequested(object? sender, PedidoDto p)     => Vm.ListoCommand.Execute(p);
    private void OnEntregarRequested(object? sender, PedidoDto p)  => Vm.EntregarCommand.Execute(p);
    private void OnCancelarRequested(object? sender, PedidoDto p)  => Vm.CancelarCommand.Execute(p);
}
