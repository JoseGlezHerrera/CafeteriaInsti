using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;
using System.ComponentModel;

namespace CafeIES.MAUI.Views;

public partial class PedidosPage : ContentPage
{
    private readonly PedidosViewModel _vm;
    private const string PendingPiKey = "pending_pi_v1";
    private PropertyChangedEventHandler? _vmPropertyChangedHandler;

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
        PedidosList.Children.Clear();
        base.OnAppearing();

        // Garantizar UNA sola suscripción a PropertyChanged del ViewModel
        if (_vmPropertyChangedHandler != null)
            ((INotifyPropertyChanged)_vm).PropertyChanged -= _vmPropertyChangedHandler;
        _vmPropertyChangedHandler = OnVmPropertyChanged;
        ((INotifyPropertyChanged)_vm).PropertyChanged += _vmPropertyChangedHandler;

        _vm.Resubscribe();
        _vm.CargarCommand.PropertyChanged -= OnCargarCommandPropertyChanged;
        _vm.CargarCommand.PropertyChanged += OnCargarCommandPropertyChanged;

        Dispatcher.Dispatch(() =>
        {
            if (_vm.CargarCommand.IsRunning) return;
            StartSkeletonAnimation();
            _vm.CargarCommand.Execute(null);
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_vmPropertyChangedHandler != null)
        {
            ((INotifyPropertyChanged)_vm).PropertyChanged -= _vmPropertyChangedHandler;
            _vmPropertyChangedHandler = null;
        }
        _vm.CargarCommand.PropertyChanged -= OnCargarCommandPropertyChanged;
        _vm.Cleanup();
        this.AbortAnimation("skeletonPedidos");
        _vm.LimpiarPedidos();
        PedidosList.Children.Clear();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PedidosViewModel.SinPedidos))
            RebuildList();
    }

    private void RebuildList()
    {
        PedidosList.Children.Clear();
        if (!Resources.TryGetValue("PedidoCardTemplate", out var res) || res is not DataTemplate template)
            return;
        foreach (var pedido in _vm.Pedidos)
        {
            var card = (View)template.CreateContent();
            card.BindingContext = pedido;
            card.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = _vm.VerDetallePedidoCommand,
                CommandParameter = pedido
            });
            PedidosList.Children.Add(card);
        }
    }

    private void OnCargarCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommunityToolkit.Mvvm.Input.IAsyncRelayCommand.IsRunning))
        {
            if (_vm.CargarCommand.IsRunning)
                StartSkeletonAnimation();
            else
                this.AbortAnimation("skeletonPedidos");
            // RebuildList() NO se llama aquí — AplicarFiltro() ya notificó SinPedidos
            // antes de que IsRunning se ponga a false, por lo que OnVmPropertyChanged
            // ya habrá reconstruido la lista.
        }
    }

    private void StartSkeletonAnimation()
    {
        this.AbortAnimation("skeletonPedidos");
        var anim = new Animation(v => SkeletonPedidos.Opacity = v, 0.35, 1.0);
        anim.Commit(this, "skeletonPedidos", length: 900, easing: Easing.SinInOut, repeat: () => true);
    }
}
