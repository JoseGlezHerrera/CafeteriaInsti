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
    [ObservableProperty] private string _notas = string.Empty;
    [ObservableProperty] private MetodoPago _metodoPago = MetodoPago.Tarjeta;

    // ── Formulario tarjeta ────────────────────────────────────────────────────
    [ObservableProperty] private string _cardNumber = string.Empty;
    [ObservableProperty] private string _cardExpMonth = string.Empty;
    [ObservableProperty] private string _cardExpYear = string.Empty;
    [ObservableProperty] private string _cardCvc = string.Empty;

    [ObservableProperty] private string _errorPago = string.Empty;
    [ObservableProperty] private bool   _hayErrorPago;
    [ObservableProperty] private string _estadoPago = string.Empty;

    public ObservableCollection<ItemCarrito> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Total), nameof(CarritoVacio))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarPedidoCommand))]
    private int _totalItems;

    public decimal Total => Items.Sum(i => i.Subtotal);
    public bool CarritoVacio => !Items.Any();

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
            Items.Add(new ItemCarrito
            {
                ProductoId  = producto.Id,
                Nombre      = producto.Nombre,
                Precio      = producto.Precio,
                Cantidad    = 1
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
        if (item.Cantidad > 1)
        {
            item.Cantidad--;
        }
        else
        {
            Items.Remove(item);
        }
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

    // ── Confirmar pedido con pago Stripe ──────────────────────────────────────
    [RelayCommand(CanExecute = nameof(PuedeConfirmar))]
    private async Task ConfirmarPedidoAsync()
    {
        HayErrorPago = false;
        ErrorPago = string.Empty;

        // Validar datos de tarjeta
        if (string.IsNullOrWhiteSpace(CardNumber) || CardNumber.Length < 13)
        {
            HayErrorPago = true;
            ErrorPago = "Introduce un número de tarjeta válido.";
            return;
        }
        if (string.IsNullOrWhiteSpace(CardExpMonth) || string.IsNullOrWhiteSpace(CardExpYear))
        {
            HayErrorPago = true;
            ErrorPago = "Introduce la fecha de caducidad.";
            return;
        }
        if (string.IsNullOrWhiteSpace(CardCvc) || CardCvc.Length < 3)
        {
            HayErrorPago = true;
            ErrorPago = "Introduce el código CVC.";
            return;
        }

        IsLoading = true;
        EstadoPago = "Procesando pago...";

        // 1. Crear PaymentIntent en nuestra API
        var pagoReq = new CrearPagoRequest(
            Items.Select(i => new LineaPedidoRequest(i.ProductoId, i.Cantidad)).ToList(),
            string.IsNullOrWhiteSpace(Notas) ? null : Notas);

        var intent = await _api.CrearPagoIntentAsync(pagoReq);
        if (intent is null)
        {
            IsLoading = false;
            EstadoPago = string.Empty;
            HayErrorPago = true;
            ErrorPago = "No se pudo iniciar el pago. Comprueba tu conexión o el horario.";
            return;
        }

        EstadoPago = "Confirmando con el banco...";

        // 3. Confirmar pago (server-side, usa secret key)
        var (pagado, errorStripe) = await _api.ConfirmarPagoAsync(
            intent.PaymentIntentId,
            CardNumber.Replace(" ", ""), CardExpMonth, CardExpYear, CardCvc);

        if (!pagado)
        {
            IsLoading = false;
            EstadoPago = string.Empty;
            HayErrorPago = true;
            ErrorPago = string.IsNullOrEmpty(errorStripe)
                ? "El pago fue rechazado. Comprueba los datos de tu tarjeta."
                : errorStripe;
            return;
        }

        EstadoPago = "Pago confirmado ✓ Creando pedido...";

        // 4. Crear pedido en nuestra API (con la referencia de Stripe)
        var pedidoReq = new CrearPedidoRequest(
            Items.Select(i => new LineaPedidoRequest(i.ProductoId, i.Cantidad)).ToList(),
            MetodoPago,
            string.IsNullOrWhiteSpace(Notas) ? null : Notas,
            intent.PaymentIntentId);

        var pedido = await _api.CrearPedidoAsync(pedidoReq);
        IsLoading = false;
        EstadoPago = string.Empty;

        if (pedido is null)
        {
            HayErrorPago = true;
            ErrorPago = "El pago se procesó pero hubo un error al crear el pedido. Contacta con el administrador.";
            return;
        }

        // 5. Limpiar carrito y navegar
        Items.Clear();
        TotalItems = 0;
        OnPropertyChanged(nameof(Total));
        CardNumber = string.Empty;
        CardExpMonth = string.Empty;
        CardExpYear = string.Empty;
        CardCvc = string.Empty;

        var totalStr = pedido.Total.ToString("F2", CultureInfo.InvariantCulture);
        await Shell.Current.GoToAsync($"ConfirmacionPedido?numeroPedido={pedido.NumeroPedido}&total={totalStr}");
    }

    private bool PuedeConfirmar() => !CarritoVacio && !IsLoading;
}

// ── Modelo de item del carrito ────────────────────────────────────────────────

public partial class ItemCarrito : ObservableObject
{
    public int     ProductoId { get; set; }
    public string  Nombre     { get; set; } = string.Empty;
    public decimal Precio     { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private int _cantidad;

    public decimal Subtotal => Precio * Cantidad;
}
