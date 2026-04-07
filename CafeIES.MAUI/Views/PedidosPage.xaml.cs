using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;
using System.ComponentModel;

namespace CafeIES.MAUI.Views;

public partial class PedidosPage : ContentPage
{
    private readonly PedidosViewModel _vm;
    private const string PendingPiKey = "pending_pi_v1";
    private PropertyChangedEventHandler? _vmPropertyChangedHandler;

    // Secuencia de carga: cada OnAppearing incrementa el número.
    // La lambda de Dispatcher.Dispatch captura el valor en el momento en que se
    // creó; si OnAppearing vuelve a dispararse antes de que la lambda ejecute
    // (doble OnAppearing, bug conocido de MAUI Shell en Android), la lambda
    // obsoleta detecta que la secuencia ya avanzó y sale sin hacer nada.
    private int _loadSequence;

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
        PedidosContainer.Content = null;   // Vaciar árbol visual de forma atómica
        base.OnAppearing();

        // Garantizar UNA sola suscripción a PropertyChanged del ViewModel
        if (_vmPropertyChangedHandler != null)
            ((INotifyPropertyChanged)_vm).PropertyChanged -= _vmPropertyChangedHandler;
        _vmPropertyChangedHandler = OnVmPropertyChanged;
        ((INotifyPropertyChanged)_vm).PropertyChanged += _vmPropertyChangedHandler;

        _vm.Resubscribe();
        _vm.CargarCommand.PropertyChanged -= OnCargarCommandPropertyChanged;
        _vm.CargarCommand.PropertyChanged += OnCargarCommandPropertyChanged;

        // Capturamos la secuencia actual para que esta lambda sea la única que ejecute
        // aunque OnAppearing dispare varias veces antes de que la lambda llegue a correr.
        var seq = ++_loadSequence;
        Dispatcher.Dispatch(() =>
        {
            if (_loadSequence != seq) return;   // OnAppearing posterior ya tomó el control
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
        PedidosContainer.Content = null;   // Vaciar árbol visual al salir
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // SinPedidos se notifica en AplicarFiltro() una vez que los datos están listos:
        // es la señal de que la lista cambió (carga inicial, "Cargar más" o cambio de filtro).
        if (e.PropertyName == nameof(PedidosViewModel.SinPedidos))
            RebuildList();
    }

    /// <summary>
    /// Reconstruye la lista de pedidos de forma atómica: crea un nuevo VerticalStackLayout
    /// con todos los ítems y lo asigna a PedidosContainer.Content en una sola operación.
    /// Esto evita el bug de MAUI Android donde Children.Clear() + Children.Add() en el
    /// mismo ciclo deja views nativos huérfanos en el ViewGroup → duplicación visual.
    /// </summary>
    private void RebuildList()
    {
        if (!Resources.TryGetValue("PedidoCardTemplate", out var res) || res is not DataTemplate template)
        {
            PedidosContainer.Content = null;
            return;
        }

        var vsl = new VerticalStackLayout { Spacing = 0 };
        foreach (var pedido in _vm.Pedidos)
        {
            var card = (View)template.CreateContent();
            card.BindingContext = pedido;
            card.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = _vm.VerDetallePedidoCommand,
                CommandParameter = pedido
            });
            vsl.Children.Add(card);
        }

        // Asignación atómica: un solo cambio de referencia → MAUI trata el árbol
        // viejo y el nuevo como una sustitución completa, sin dejar views colgados.
        PedidosContainer.Content = vsl;
    }

    private void OnCargarCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommunityToolkit.Mvvm.Input.IAsyncRelayCommand.IsRunning))
        {
            if (_vm.CargarCommand.IsRunning)
                StartSkeletonAnimation();
            else
                this.AbortAnimation("skeletonPedidos");
        }
    }

    private void StartSkeletonAnimation()
    {
        this.AbortAnimation("skeletonPedidos");
        var anim = new Animation(v => SkeletonPedidos.Opacity = v, 0.35, 1.0);
        anim.Commit(this, "skeletonPedidos", length: 900, easing: Easing.SinInOut, repeat: () => true);
    }
}
