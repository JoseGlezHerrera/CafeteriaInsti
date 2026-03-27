using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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

        // Limpiar carrito si la sesión expira (refresh token caducado)
        WeakReferenceMessenger.Default.Register<SesionExpiradaMessage>(this, (_, _) =>
            MainThread.BeginInvokeOnMainThread(LimpiarCarrito));
    }

    // ── Estado ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarPedidoCommand))]
    private bool   _isLoading;
    [ObservableProperty] private string _notas      = string.Empty;
    [ObservableProperty] private string _errorPago  = string.Empty;
    [ObservableProperty] private bool   _hayErrorPago;
    [ObservableProperty] private string _estadoPago = string.Empty;

    // ── Desayuno gratuito ─────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensajeDesayuno))]
    private bool _zumoDisponible;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensajeDesayuno))]
    private bool _bocataDisponible;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MensajeDesayuno), nameof(TotalEfectivo))]
    private bool _tieneDesayunoGratuito;

    /// <summary>Texto del banner de desayuno gratuito en la UI.</summary>
    public string MensajeDesayuno
    {
        get
        {
            if (!TieneDesayunoGratuito) return string.Empty;
            if (!ZumoDisponible && !BocataDisponible) return "Has usado tu desayuno gratuito de hoy";
            if (ZumoDisponible && BocataDisponible)   return "Desayuno gratuito disponible: 1 zumo + 1 bocata";
            if (ZumoDisponible)                        return "Desayuno gratuito disponible: 1 zumo";
            return "Desayuno gratuito disponible: 1 bocata";
        }
    }

    public bool HayDesayunoDisponible => TieneDesayunoGratuito && (ZumoDisponible || BocataDisponible);

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

    /// <summary>
    /// Total tras aplicar los descuentos de desayuno gratuito (lo que realmente se cobra).
    /// Si el usuario no es beneficiario, coincide con Total.
    /// </summary>
    public decimal TotalEfectivo
    {
        get
        {
            if (!TieneDesayunoGratuito) return Total;
            decimal total = 0;
            bool zumoAplicado   = !ZumoDisponible;
            bool bocataAplicado = !BocataDisponible;
            foreach (var item in Items)
            {
                decimal precio = item.Precio;
                if (!zumoAplicado && item.ComponenteDesayuno == ComponenteDesayuno.Zumo)
                {
                    precio = 0; zumoAplicado = true;
                }
                else if (!bocataAplicado && item.ComponenteDesayuno == ComponenteDesayuno.Bocata)
                {
                    precio = 0; bocataAplicado = true;
                }
                total += precio * item.Cantidad;
            }
            return total;
        }
    }

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
                ProductoId         = producto.Id,
                Nombre             = producto.Nombre,
                Precio             = producto.Precio,
                Cantidad           = 1,
                ImagenUrl          = imageUrl,
                ComponenteDesayuno = producto.ComponenteDesayuno
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

    // ── Paso 1: crear intent y navegar al WebView de Stripe (o flujo gratuito) ─
    [RelayCommand(CanExecute = nameof(PuedeConfirmar))]
    private async Task ConfirmarPedidoAsync()
    {
        HayErrorPago = false;
        ErrorPago    = string.Empty;
        IsLoading    = true;
        EstadoPago   = "Iniciando pago…";

        var lineas = Items.Select(i => new LineaPedidoRequest(i.ProductoId, i.Cantidad)).ToList();
        var notas  = string.IsNullOrWhiteSpace(Notas) ? null : Notas;

        // ── Flujo gratuito: el pedido es completamente gratis ───────────────
        if (TieneDesayunoGratuito && TotalEfectivo == 0)
        {
            var req = new CrearPedidoRequest(lineas, MetodoPago.Gratuito, notas, null);
            var (pedido, errorPedido) = await _api.CrearPedidoAsync(req);

            IsLoading = false; EstadoPago = string.Empty;

            if (pedido is null)
            {
                HayErrorPago = true;
                ErrorPago = errorPedido ?? "No se pudo crear el pedido. Inténtalo de nuevo.";
                return;
            }

            var totalStr = Total.ToString("F2", CultureInfo.InvariantCulture);
            // Usamos "gratuito-{NumeroPedido}" para que ConfirmacionPedidoPage muestre
            // el número directamente sin necesidad de polling a la API.
            LimpiarCarrito();
            await Shell.Current.GoToAsync(
                $"ConfirmacionPedido?paymentIntentId=gratuito-{pedido.NumeroPedido}&total={totalStr}");
            await CargarDesayunoStatusAsync();
            return;
        }

        // ── Flujo Stripe ────────────────────────────────────────────────────
        var (intent, errorIntent) = await _api.CrearPagoIntentAsync(new CrearPagoRequest(lineas, notas));

        if (intent is null)
        {
            IsLoading = false; EstadoPago = string.Empty;
            HayErrorPago = true;
            ErrorPago = errorIntent ?? "No se pudo iniciar el pago. Comprueba tu conexión o el horario.";
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

    /// <summary>Carga el estado del desayuno gratuito del usuario desde la API.</summary>
    public async Task CargarDesayunoStatusAsync()
    {
        var status = await _api.GetDesayunoStatusAsync();
        if (status is null) return;
        TieneDesayunoGratuito = status.TieneDesayunoGratuito;
        ZumoDisponible        = status.ZumoDisponible;
        BocataDisponible      = status.BocataDisponible;
        OnPropertyChanged(nameof(TotalEfectivo));
    }

    // ── Paso 2: llamado por PagamentoWebPage tras el pago exitoso ────────────
    /// <summary>
    /// Navega a la confirmación INMEDIATAMENTE (el pago ya fue cobrado) y crea
    /// el pedido en background. El número de pedido aparecerá cuando el servidor responda.
    /// </summary>
    public async Task FinalizarPagoAsync(string paymentIntentId)
    {
        // Capturar estado antes de limpiar
        var totalCarrito = Total;
        var lineas       = _pendingLineas.ToList();
        var notas        = _pendingNotas;

        // Limpiar carrito y estado pendiente INMEDIATAMENTE
        Items.Clear();
        TotalItems = 0;
        OnPropertyChanged(nameof(Total));
        Notas                  = string.Empty;
        PendingClientSecret    = string.Empty;
        PendingPublishableKey  = string.Empty;
        PendingPaymentIntentId = string.Empty;
        _pendingLineas         = new();
        _pendingNotas          = null;
        HayErrorPago           = false;
        ErrorPago              = string.Empty;
        IsLoading              = false;
        EstadoPago             = string.Empty;

        // Navegar a confirmación sin esperar al servidor
        var totalStr = totalCarrito.ToString("F2", CultureInfo.InvariantCulture);
        await Shell.Current.GoToAsync(
            $"ConfirmacionPedido?paymentIntentId={Uri.EscapeDataString(paymentIntentId)}&total={totalStr}");

        // Crear pedido en background — el servidor también lo crea vía webhook de Stripe
        _ = Task.Run(async () =>
        {
            try
            {
                var req = new CrearPedidoRequest(lineas, MetodoPago.Tarjeta, notas, paymentIntentId);
                await _api.CrearPedidoAsync(req);
            }
            catch { /* best-effort: el webhook de Stripe es el respaldo */ }
        });
    }

    /// <summary>
    /// Cancela el intento de pago en curso sin tocar los items del carrito.
    /// Llamar cuando el usuario abandona la pantalla de pago (vuelve atrás o cambia de tab).
    /// </summary>
    public void CancelarPendingPago()
    {
        // FIX-10: Cancelar el PaymentIntent en Stripe en background
        if (!string.IsNullOrEmpty(PendingPaymentIntentId))
            _ = _api.CancelarPagoIntentAsync(PendingPaymentIntentId);

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
    public int                ProductoId         { get; set; }
    public string             Nombre             { get; set; } = string.Empty;
    public decimal            Precio             { get; set; }
    public string?            ImagenUrl          { get; set; }
    public ComponenteDesayuno ComponenteDesayuno { get; set; } = ComponenteDesayuno.Ninguno;
    public bool               TieneImagen => !string.IsNullOrEmpty(ImagenUrl);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private int _cantidad;

    public decimal Subtotal => Precio * Cantidad;
}
