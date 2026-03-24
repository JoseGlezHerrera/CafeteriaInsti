using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace CafeIES.MAUI.ViewModels;

[QueryProperty(nameof(ProductoId), "productoId")]
public partial class ProductoDetalleViewModel : ObservableObject
{
    private readonly ApiService      _api;
    private readonly CarritoViewModel _carrito;

    public ProductoDetalleViewModel(ApiService api, CarritoViewModel carrito)
    {
        _api     = api;
        _carrito = carrito;
    }

    [ObservableProperty] private int         _productoId;
    [ObservableProperty] private bool        _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneProducto))]
    [NotifyPropertyChangedFor(nameof(TieneDescripcion))]
    [NotifyPropertyChangedFor(nameof(AlergenosTexto))]
    [NotifyPropertyChangedFor(nameof(PuedeAnadir))]
    private ProductoDto? _producto;

    public bool  TieneProducto   => Producto is not null;
    public bool  TieneDescripcion => !string.IsNullOrWhiteSpace(Producto?.Descripcion);
    public bool  PuedeAnadir     => Producto is not null && Producto.NivelStock != "agotado";

    /// <summary>Texto con emojis y nombres de todos los alérgenos.</summary>
    public string AlergenosTexto =>
        Producto?.Alergenos.Count > 0
            ? string.Join("  ", Producto.Alergenos.Select(a => $"{a.Emoji} {a.Nombre}"))
            : string.Empty;

    partial void OnProductoIdChanged(int value)
    {
        if (value > 0) _ = CargarAsync();
    }

    [RelayCommand]
    public async Task CargarAsync()
    {
        if (ProductoId <= 0) return;

        IsLoading = true;
        Producto  = await _api.GetProductoByIdAsync(ProductoId);
        IsLoading = false;
    }

    [RelayCommand]
    private async Task AnadirAlCarritoAsync()
    {
        if (Producto is null) return;

        if (Producto.NivelStock == "agotado")
        {
            await Shell.Current.DisplayAlert(
                "Producto agotado",
                $"'{Producto.Nombre}' no tiene stock disponible.",
                "OK");
            return;
        }

        _carrito.AnadirProducto(Producto);

        try
        {
            var toast = Toast.Make($"✓ {Producto.Nombre} añadido", ToastDuration.Short, 14);
            await toast.Show();
        }
        catch { /* Toast no disponible en esta plataforma/configuración */ }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task VolverAsync()
        => await Shell.Current.GoToAsync("..");
}
