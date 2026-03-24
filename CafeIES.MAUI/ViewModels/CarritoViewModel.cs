using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class CarritoViewModel : ObservableObject
{
    private readonly ApiService _api;

    public CarritoViewModel(ApiService api)
    {
        _api = api;
    }

    // ── Estado ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarPedidoCommand))]
    private bool   _isLoading;
    [ObservableProperty] private string _notas      = string.Empty;
    [ObservableProperty] private string _errorPago  = string.Empty;
    [ObservableProperty] private bool   _hayErrorPago;
    [ObservableProperty] private string _estadoPago = string.Empty;

    // ── Datos pendientes de pago (establecidos antes de navegar al WebView) ───
    public string PendingClientSecret    { get; private set; } = string.Empty;
    public string PendingPublishableKey  { get; private set; } = string.Empty;
    public string PendingPaymentIntentId { get; private set; } = string.Empty;
    private List<LineaPedidoRequest> _pendingLineas = new();
    private string? _pendingNotas;

    public ObservableCollection<ItemCarrito> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Total), nameof(CarritoVacio))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarPedidoCommand))]
    private int _totalItems;

    public decimal Total      => Items.Sum(i => i.Subtotal);
    public bool   CarritoVacio => !Items.Any();

    // ── Gestión del carrito ───────────────────────────────────────────────────

    public void AnadirProducto(ProductoDto producto)
    {
        var existente = Items.FirstOrDefault(i => i.ProductoId == producto.Id);
        if (existente is not null)
        {
            if (existente.Cantidad >= 20) return;
            existente.Cantidad++;
        }
        else
        {
            var imageUrl = string.IsNullOrEmpty(producto.ImagenUrl) ? null
                         : producto.ImagenUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                           ? producto.ImagenUrl
                           : _api.BuildImageUrl(producto.ImagenUrl);
            Items.Add(new ItemCarrito
            {
                ProductoId = producto.Id,
                Nombre     = producto.Nombre,
                Precio     = producto.Precio,
                Cantidad   = 1,
                ImagenUrl  = imageUrl
            });
        }
        TotalItems = Items.Sum(i => i.Cantidad);
        OnPropertyChanged(nameof(Total));
    }

    [RelayCommand]
    private void IncrementarCantidad(ItemCarrito item)
    {
        if (item.Cantidad >= 20) return;
        item.Cantidad++;
        TotalItems = Items.Sum(i => i.Cantidad);
        OnPropertyChanged(nameof(Total));
    }

    [RelayCommand]
    private void DecrementarCantidad(ItemCarrito item)
    {
        if (item.Cantidad > 1) item.Cantidad--;
        else Items.Remove(item);
        TotalItems = Items.Sum(i => i.Cantidad);
        OnPropertyChanged(nameof(Total));
    }

    [RelayCommand]
    private void EliminarItem(ItemCarrito item)
    {
        Items.Remove(item);
        TotalItems = Items.Sum(i => i.Cantidad);
        OnPropertyChanged(nameof(Total));
    }

    // ── Paso 1: crear intent y navegar al WebView de Stripe ──────────────────
    [RelayCommand(CanExecute = nameof(PuedeConfirmar))]
    private async Task ConfirmarPedidoAsync()
    {
        HayErrorPago = false;
        ErrorPago    = string.Empty;
        IsLoading    = true;
        EstadoPago   = "Comprobando horario…";

        // Validar horario antes de crear el PaymentIntent
        var horario = await _api.GetHorarioStatusAsync();
        if (horario is not null && !horario.PuedePedir)
        {
            IsLoading = false; EstadoPago = string.Empty;
            HayErrorPago = true;
            ErrorPago = horario.Mensaje;
            return;
        }

        EstadoPago = "Iniciando pago…";

        var lineas  = Items.Select(i => new LineaPedidoRequest(i.ProductoId, i.Cantidad)).ToList();
        var notas   = string.IsNullOrWhiteSpace(Notas) ? null : Notas;
        var intent  = await _api.CrearPagoIntentAsync(new CrearPagoRequest(lineas, notas));

        if (intent is null)
        {
            IsLoading = false; EstadoPago = string.Empty;
            HayErrorPago = true;
            ErrorPago = "No se pudo iniciar el pago. Comprueba tu conexión o el horario.";
            return;
        }

        var config = await _api.GetStripeConfigAsync();
        if (config is null || string.IsNullOrEmpty(config.PublishableKey))
        {
            IsLoading = false; EstadoPago = string.Empty;
            HayErrorPago = true;
            ErrorPago = "Error de configuración de pago. Contacta con el administrador.";
            return;
        }

        // Guardar estado pendiente para usarlo tras el pago
        _pendingLineas         = lineas;
        _pendingNotas          = notas;
        PendingClientSecret    = intent.ClientSecret;
        PendingPublishableKey  = config.PublishableKey;
        PendingPaymentIntentId = intent.PaymentIntentId;

        IsLoading = false; EstadoPago = string.Empty;

        await Shell.Current.GoToAsync("PagamentoWeb");
    }

    // ── Paso 2: llamado por PagamentoWebPage tras el pago exitoso ────────────
    /// <summary>Crea el pedido y navega a la confirmación. Llamar desde el hilo principal.</summary>
    public async Task FinalizarPagoAsync(string paymentIntentId)
    {
        IsLoading  = true;
        EstadoPago = "Creando pedido…";

        var pedidoReq = new CrearPedidoRequest(
            _pendingLineas,
            MetodoPago.Tarjeta,
            _pendingNotas,
            paymentIntentId);

        var pedido = await _api.CrearPedidoAsync(pedidoReq);
        IsLoading = false; EstadoPago = string.Empty;

        if (pedido is null)
        {
            HayErrorPago = true;
            ErrorPago = "El pago se procesó pero hubo un error al crear el pedido. Contacta con el administrador.";
            return;
        }

        // Limpiar carrito
        Items.Clear();
        TotalItems = 0;
        OnPropertyChanged(nameof(Total));
        Notas = string.Empty;

        var totalStr = pedido.Total.ToString("F2", CultureInfo.InvariantCulture);
        await Shell.Current.GoToAsync(
            $"ConfirmacionPedido?numeroPedido={pedido.NumeroPedido}&total={totalStr}");
    }

    /// <summary>
    /// Cancela el intento de pago en curso sin tocar los items del carrito.
    /// Llamar cuando el usuario abandona la pantalla de pago (vuelve atrás o cambia de tab).
    /// </summary>
    public void CancelarPendingPago()
    {
        PendingClientSecret    = string.Empty;
        PendingPublishableKey  = string.Empty;
        PendingPaymentIntentId = string.Empty;
        _pendingLineas = new();
        _pendingNotas  = null;
        HayErrorPago  = false;
        ErrorPago     = string.Empty;
        EstadoPago    = string.Empty;
        IsLoading     = false;
    }

    /// <summary>Limpia el carrito y el estado de pago pendiente. Llamar al cerrar sesión.</summary>
    public void LimpiarCarrito()
    {
        Items.Clear();
        TotalItems = 0;
        Notas = string.Empty;
        CancelarPendingPago();
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(CarritoVacio));
    }

    private bool PuedeConfirmar() => !CarritoVacio && !IsLoading;
}

// ── Modelo de item del carrito ────────────────────────────────────────────────

public partial class ItemCarrito : ObservableObject
{
    public int     ProductoId { get; set; }
    public string  Nombre     { get; set; } = string.Empty;
    public decimal Precio     { get; set; }
    public string? ImagenUrl  { get; set; }
    public bool    TieneImagen => !string.IsNullOrEmpty(ImagenUrl);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private int _cantidad;

    public decimal Subtotal => Precio * Cantidad;
}
