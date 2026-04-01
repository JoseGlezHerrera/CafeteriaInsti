using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class AdminPedidosPage : ContentPage
{
    private AdminPedidosViewModel Vm => (AdminPedidosViewModel)BindingContext;

    public AdminPedidosPage(AdminPedidosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Resubscribir mensajes WeakReference al volver a la página (tab cacheado).
        // La carga la dispara el EventToCommandBehavior del XAML — no llamar aquí para evitar doble petición.
        Vm.Resubscribe();
    }

    // FIX-11: Desuscribir mensajes al desaparecer la página
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Vm.Cleanup();
    }

    private void OnPrepararClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is PedidoDto p)
            Vm.PrepararCommand.Execute(p);
    }

    private void OnListoClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is PedidoDto p)
            Vm.ListoCommand.Execute(p);
    }

    private void OnEntregarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is PedidoDto p)
            Vm.EntregarCommand.Execute(p);
    }

    private void OnCancelarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is PedidoDto p)
            Vm.CancelarCommand.Execute(p);
    }
}
