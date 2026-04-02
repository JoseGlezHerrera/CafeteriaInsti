using CafeIES.MAUI.ViewModels;

namespace CafeIES.MAUI.Views;

public partial class PedidosPage : ContentPage
{
    private readonly PedidosViewModel _vm;
    // FIX-PI: clave debe coincidir con CarritoViewModel.PendingPiKey
    private const string PendingPiKey = "pending_pi_v1";

    public PedidosPage(PedidosViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // FIX-PI: Si la app se cerró durante un pago (entre la confirmación de Stripe y la
        // creación del pedido en BD), el webhook habrá creado el pedido. Al volver a la app,
        // redirigimos a ConfirmacionPedidoPage para que el usuario vea el resultado.
        var pendingPi = Preferences.Default.Get(PendingPiKey, string.Empty);
        if (!string.IsNullOrEmpty(pendingPi))
        {
            Preferences.Default.Remove(PendingPiKey);
            Dispatcher.Dispatch(async () =>
            {
                if (Shell.Current is null) return;
                await Shell.Current.GoToAsync(
                    $"ConfirmacionPedido?paymentIntentId={Uri.EscapeDataString(pendingPi)}&total=0.00");
            });
            return;
        }

        _vm.Resubscribe();
    }

    // FIX-11: Desuscribir mensajes al desaparecer la página
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Cleanup();
    }
}
