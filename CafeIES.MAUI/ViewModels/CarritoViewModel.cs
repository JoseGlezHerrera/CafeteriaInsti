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

    // ── Confirmar pedido ──────────────────────────────────────────────────────
    [RelayCommand(CanExecute = nameof(PuedeConfirmar))]
    private async Task ConfirmarPedidoAsync()
    {
        IsLoading = true;

        var request = new CrearPedidoRequest(
            Items.Select(i => new LineaPedidoRequest(i.ProductoId, i.Cantidad)).ToList(),
            MetodoPago,
            string.IsNullOrWhiteSpace(Notas) ? null : Notas
        );

        var pedido = await _api.CrearPedidoAsync(request);
        IsLoading = false;

        if (pedido is null)
        {
            await Shell.Current.DisplayAlert(
                "Error",
                "No se pudo realizar el pedido. Comprueba tu conexión o el horario.",
                "OK");
            return;
        }

        // Limpiar carrito
        Items.Clear();
        TotalItems = 0;
        OnPropertyChanged(nameof(Total));

        // Navegar a confirmación — total con cultura invariante para evitar problemas con coma decimal
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
