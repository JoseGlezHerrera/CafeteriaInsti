using CafeIES.MAUI.Controls;
using CafeIES.MAUI.Services;
using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class EmpleadoPedidosPage : ContentPage
{
    private EmpleadoPedidosViewModel Vm => (EmpleadoPedidosViewModel)BindingContext;

    private readonly IPrintService _print;   // se conserva (por si se usa en AdminPedidosPage u otro sitio)
    private readonly EscPosPrinterService _escPos;  // impresión directa TCP a la AVPos TC300

    public EmpleadoPedidosPage(EmpleadoPedidosViewModel vm, IPrintService print, EscPosPrinterService escPos)
    {
        InitializeComponent();
        BindingContext = vm;
        _print = print;
        _escPos = escPos;
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

    private async void OnImprimirRequested(object? sender, PedidoDto p)
    {
        var fresco = await Vm.ObtenerParaImpresionAsync(p.Id) ?? p;
        try
        {
            await _escPos.ImprimirAsync(fresco);
        }
        catch (TimeoutException)
        {
            await DisplayAlert("Sin conexión",
                $"No se pudo conectar con la impresora en {EscPosPrinterService.IpImpresora}:{EscPosPrinterService.PuertoImpresora}.\n" +
                "Comprueba que está encendida y en la red.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de impresión", ex.Message, "OK");
        }
    }

    private void OnPrepararRequested(object? sender, PedidoDto p) => Vm.PrepararCommand.Execute(p);
    private void OnListoRequested(object? sender, PedidoDto p) => Vm.ListoCommand.Execute(p);
    private void OnEntregarRequested(object? sender, PedidoDto p) => Vm.EntregarCommand.Execute(p);
    private void OnCancelarRequested(object? sender, PedidoDto p) => Vm.CancelarCommand.Execute(p);
}