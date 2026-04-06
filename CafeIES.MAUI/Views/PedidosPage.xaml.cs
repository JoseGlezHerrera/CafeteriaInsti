using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

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
        // FIX-PI: Si la app se cerró durante un pago (entre la confirmación de Stripe y la
        // creación del pedido en BD), el webhook habrá creado el pedido. Al volver a la app,
        // redirigimos a ConfirmacionPedidoPage para que el usuario vea el resultado.
        var pendingPi = Preferences.Default.Get(PendingPiKey, string.Empty);
        if (!string.IsNullOrEmpty(pendingPi))
        {
            Preferences.Default.Remove(PendingPiKey);
            base.OnAppearing();
            Dispatcher.Dispatch(async () =>
            {
                if (Shell.Current is null) return;
                await Shell.Current.GoToAsync(
                    $"ConfirmacionPedido?paymentIntentId={Uri.EscapeDataString(pendingPi)}&total=0.00");
            });
            return;
        }

        _vm.LimpiarPedidos();
        base.OnAppearing();

        // WORKAROUND-DUP: MAUI Shell acumula suscripciones a CollectionChanged al
        // re-adjuntar la página del tab cacheado. Cada Pedidos.Add() dispararía el
        // handler N veces → N vistas por ítem → duplicación visual.
        // SetItemsSource(null) fuerza la desuscripción de todas las suscripciones
        // acumuladas; SetItemsSource(Pedidos) crea UNA suscripción limpia.
        // Mismo patrón que CarritoPage (probado y funcional).
        BindableLayout.SetItemsSource(PedidosList, null);
        BindableLayout.SetItemsSource(PedidosList, _vm.Pedidos);

        _vm.Resubscribe();
        _vm.CargarCommand.PropertyChanged += OnCargarCommandPropertyChanged;

        Dispatcher.Dispatch(() =>
        {
            if (_vm.CargarCommand.IsRunning) return;
            StartSkeletonAnimation();
            _vm.CargarCommand.Execute(null);
        });
    }

    private async void OnPedidoTapped(object sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is PedidoDto pedido)
            await _vm.VerDetallePedidoCommand.ExecuteAsync(pedido);
    }

    // FIX-11: Desuscribir mensajes al desaparecer la página
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.CargarCommand.PropertyChanged -= OnCargarCommandPropertyChanged;
        _vm.Cleanup();
        this.AbortAnimation("skeletonPedidos");
        // FIX-DUP: vaciar datos para que CollectionView empiece limpio al volver;
        // sin esto, MAUI mantiene los ítems en caché visual y los duplica al recargar.
        _vm.LimpiarPedidos();
    }

    private void OnCargarCommandPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommunityToolkit.Mvvm.Input.IAsyncRelayCommand.IsRunning))
        {
            if (_vm.CargarCommand.IsRunning) StartSkeletonAnimation();
            else this.AbortAnimation("skeletonPedidos");
        }
    }

    private void StartSkeletonAnimation()
    {
        this.AbortAnimation("skeletonPedidos");
        var anim = new Animation(v => SkeletonPedidos.Opacity = v, 0.35, 1.0);
        anim.Commit(this, "skeletonPedidos", length: 900, easing: Easing.SinInOut, repeat: () => true);
    }
}
