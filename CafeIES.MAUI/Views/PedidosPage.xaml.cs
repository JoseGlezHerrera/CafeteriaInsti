using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;
using System.ComponentModel;

namespace CafeIES.MAUI.Views;

public partial class PedidosPage : ContentPage
{
    private readonly PedidosViewModel _vm;
    private const string PendingPiKey = "pending_pi_v1";
    private PropertyChangedEventHandler? _vmPropertyChangedHandler;

    // Guard contra doble OnAppearing (bug MAUI Shell Android):
    // cada OnAppearing incrementa _loadSequence; la lambda de Dispatch
    // verifica que la secuencia no haya avanzado antes de ejecutar.
    private int _loadSequence;

    public PedidosPage(PedidosViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        // FIX-PI: pago interrumpido — ver si hay un PaymentIntent pendiente
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
        PedidosList.ItemsSource = null;   // Limpiar lista de forma limpia
        base.OnAppearing();

        // Garantizar UNA sola suscripción a PropertyChanged del ViewModel
        if (_vmPropertyChangedHandler != null)
            ((INotifyPropertyChanged)_vm).PropertyChanged -= _vmPropertyChangedHandler;
        _vmPropertyChangedHandler = OnVmPropertyChanged;
        ((INotifyPropertyChanged)_vm).PropertyChanged += _vmPropertyChangedHandler;

        _vm.Resubscribe();
        _vm.CargarCommand.PropertyChanged -= OnCargarCommandPropertyChanged;
        _vm.CargarCommand.PropertyChanged += OnCargarCommandPropertyChanged;

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
        PedidosList.ItemsSource = null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // SinPedidos se notifica en AplicarFiltro() cuando los datos están listos.
        // Es el único trigger de reconstrucción: carga inicial, "Cargar más" y filtro.
        if (e.PropertyName == nameof(PedidosViewModel.SinPedidos))
            RebuildList();
    }

    /// <summary>
    /// Reemplaza ItemsSource con un snapshot List&lt;T&gt; de los pedidos actuales.
    /// List&lt;T&gt; (sin INotifyCollectionChanged) garantiza que CollectionView
    /// no suscribe ningún evento de colección → imposible acumulación de handlers.
    /// </summary>
    private void RebuildList()
    {
        PedidosList.ItemsSource = new List<PedidoDto>(_vm.Pedidos);
    }

    private async void OnPedidoSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PedidoDto pedido)
        {
            PedidosList.SelectedItem = null;   // Desseleccionar para limpiar el highlight
            await _vm.VerDetallePedidoCommand.ExecuteAsync(pedido);
        }
    }

    private void OnCargarCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommunityToolkit.Mvvm.Input.IAsyncRelayCommand.IsRunning))
            return;

        if (_vm.CargarCommand.IsRunning)
        {
            StartSkeletonAnimation();
        }
        else
        {
            this.AbortAnimation("skeletonPedidos");
            // Restablecer el indicador de pull-to-refresh manualmente.
            // NO usamos IsRefreshing={Binding IsRunning} porque en Android ese binding
            // de dos vías provoca que setRefreshing(false) dispare onRefresh() en
            // SwipeRefreshLayout → segunda ejecución de CargarCommand → duplicación.
            PullToRefresh.IsRefreshing = false;
        }
    }

    private void StartSkeletonAnimation()
    {
        this.AbortAnimation("skeletonPedidos");
        var anim = new Animation(v => SkeletonPedidos.Opacity = v, 0.35, 1.0);
        anim.Commit(this, "skeletonPedidos", length: 900, easing: Easing.SinInOut, repeat: () => true);
    }
}
