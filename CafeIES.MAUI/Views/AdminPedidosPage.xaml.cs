using CafeIES.MAUI.Services;
using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class AdminPedidosPage : ContentPage
{
    private AdminPedidosViewModel Vm => (AdminPedidosViewModel)BindingContext;
    private readonly IPrintService _print;

    public AdminPedidosPage(AdminPedidosViewModel vm, IPrintService print)
    {
        InitializeComponent();
        BindingContext = vm;
        _print = print;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Vm.Resubscribe();

        Vm.CargarCommand.PropertyChanged -= OnCargarCommandPropertyChanged;
        Vm.CargarCommand.PropertyChanged += OnCargarCommandPropertyChanged;

        if (!Vm.CargarCommand.IsRunning)
            Vm.CargarCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Vm.CargarCommand.PropertyChanged -= OnCargarCommandPropertyChanged;
        Vm.Cleanup();
    }

    private void OnCargarCommandPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommunityToolkit.Mvvm.Input.IAsyncRelayCommand.IsRunning)) return;

        // Restablecer el indicador de pull-to-refresh manualmente.
        // NO se usa IsRefreshing={Binding} porque en Android setRefreshing(false)
        // dispara onRefresh() en SwipeRefreshLayout → segunda carga → duplicados.
        if (!Vm.CargarCommand.IsRunning)
            PullToRefresh.IsRefreshing = false;
    }

    private void OnImprimirRequested(object? sender, PedidoDto p) =>
        _ = _print.ImprimirAsync(TicketHtmlBuilder.Build(p), $"Pedido #{p.NumeroPedido:D3}");
    private void OnPrepararRequested(object? sender, PedidoDto p)  => Vm.PrepararCommand.Execute(p);
    private void OnListoRequested(object? sender, PedidoDto p)     => Vm.ListoCommand.Execute(p);
    private void OnEntregarRequested(object? sender, PedidoDto p)  => Vm.EntregarCommand.Execute(p);
    private void OnCancelarRequested(object? sender, PedidoDto p)  => Vm.CancelarCommand.Execute(p);
}
