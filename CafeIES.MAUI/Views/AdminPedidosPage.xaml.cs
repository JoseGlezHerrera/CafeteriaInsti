using System.Text;
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

    private async void OnImprimirRequested(object? sender, PedidoDto p)
    {
        var fresco = await Vm.ObtenerParaImpresionAsync(p.Id) ?? p;
        var resumen = BuildResumenTexto(fresco);
        var imprimir = await Shell.Current.DisplayAlert(
            $"Pedido #{fresco.NumeroPedido:D3}",
            resumen,
            "Imprimir",
            "Cerrar");
        if (imprimir)
            _ = _print.ImprimirAsync(TicketHtmlBuilder.Build(fresco), $"Pedido #{fresco.NumeroPedido:D3}");
    }

    private static string BuildResumenTexto(PedidoDto p)
    {
        var sb = new StringBuilder();
        sb.AppendLine(p.UsuarioNombre);
        foreach (var l in p.Lineas)
        {
            sb.AppendLine();
            sb.Append($"{l.ProductoNombre} x{l.Cantidad}");
            if (l.Subtotal > 0) sb.Append($"  {l.Subtotal:F2}€");
            sb.AppendLine();
            if (l.Ingredientes is { Count: > 0 })
            {
                foreach (var ing in l.Ingredientes)
                {
                    var accion = ing.Accion == AccionIngrediente.Quitar ? "sin" : "+";
                    var extra = ing.Cantidad > 1 ? $" x{ing.Cantidad}" : string.Empty;
                    sb.AppendLine($"  {accion} {ing.Nombre}{extra}");
                }
            }
            if (!string.IsNullOrWhiteSpace(l.Notas))
                sb.AppendLine($"  → {l.Notas}");
        }
        if (!string.IsNullOrWhiteSpace(p.Notas))
        {
            sb.AppendLine();
            sb.AppendLine($"NOTA: {p.Notas}");
        }
        sb.AppendLine();
        sb.Append($"Total: {p.Total:F2}€");
        return sb.ToString();
    }

    private void OnPrepararRequested(object? sender, PedidoDto p)  => Vm.PrepararCommand.Execute(p);
    private void OnListoRequested(object? sender, PedidoDto p)     => Vm.ListoCommand.Execute(p);
    private void OnEntregarRequested(object? sender, PedidoDto p)  => Vm.EntregarCommand.Execute(p);
    private void OnCancelarRequested(object? sender, PedidoDto p)  => Vm.CancelarCommand.Execute(p);
}
