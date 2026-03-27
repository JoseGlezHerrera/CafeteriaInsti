using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly CarritoViewModel _carrito;

    // Cache local (#15) — evita recargar el catálogo cada vez
    private List<ProductoDto>?  _cacheProductos;
    private List<CategoriaDto>? _cacheCategorias;
    private DateTime _cacheTimestamp = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    // FIX-12: Guardar referencia al handler para poder desuscribirse
    private readonly System.ComponentModel.PropertyChangedEventHandler _carritoHandler;

    public HomeViewModel(ApiService api, CarritoViewModel carrito)
    {
        _api = api;
        _carrito = carrito;

        ItemsEnCarrito = _carrito.TotalItems;
        _carritoHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(CarritoViewModel.TotalItems))
                ItemsEnCarrito = _carrito.TotalItems;
        };
        _carrito.PropertyChanged += _carritoHandler;
    }

    /// <summary>FIX-12: Limpia suscripciones para evitar memory leaks.</summary>
    public void Cleanup()
    {
        _carrito.PropertyChanged -= _carritoHandler;
    }

    /// <summary>BUG-4: Restaura suscripciones al volver a la página (tab cacheado).</summary>
    public void Resubscribe()
    {
        _carrito.PropertyChanged -= _carritoHandler;
        _carrito.PropertyChanged += _carritoHandler;
        // Sincronizar badge inmediatamente por si cambió mientras no estábamos suscritos
        ItemsEnCarrito = _carrito.TotalItems;
    }

    // ── Estado horario ────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _puedePedir;
    [ObservableProperty] private string _mensajeHorario    = string.Empty;
    [ObservableProperty] private string _proximaFranja     = string.Empty;
    [ObservableProperty] private bool   _mostrarBannerBloqueo;

    // ── Catálogo ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _categoriaSeleccionada = "Todo";
    [ObservableProperty] private string _busqueda = string.Empty;

    public ObservableCollection<CategoriaChipItem>  Categorias  { get; } = new();
    public ObservableCollection<ProductoDto>   Productos   { get; } = new();
    public ObservableCollection<ProductoDto>   ProductosFiltrados { get; } = new();

    // ── Carrito (badge) ───────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CarritoBadge))]
    private int _itemsEnCarrito;

    public string CarritoBadge => ItemsEnCarrito > 0 ? ItemsEnCarrito.ToString() : string.Empty;

    // ── Init ──────────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task CargarAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        var usarCache = _cacheProductos is not null
                     && _cacheCategorias is not null
                     && DateTime.UtcNow - _cacheTimestamp < CacheDuration;

        var horarioTask = _api.GetHorarioStatusAsync();
        var categoriasTask = usarCache ? Task.FromResult(_cacheCategorias!) : _api.GetCategoriasAsync();
        var productosTask  = usarCache ? Task.FromResult(_cacheProductos!)  : _api.GetProductosAsync();

        await Task.WhenAll(horarioTask, categoriasTask, productosTask);

        var horario    = horarioTask.Result;
        var categorias = categoriasTask.Result;
        var productos  = productosTask.Result;

        if (!usarCache)
        {
            _cacheCategorias  = categorias;
            _cacheProductos   = productos;
            _cacheTimestamp   = DateTime.UtcNow;
        }

        // Estado horario
        if (horario is not null)
        {
            PuedePedir          = horario.PuedePedir;
            MensajeHorario      = horario.Mensaje;
            MostrarBannerBloqueo = !horario.PuedePedir;

            if (!horario.PuedePedir && horario.ProximaHora is not null)
                ProximaFranja = $"Próxima ventana: {horario.ProximaFranja} a las {horario.ProximaHora}";
        }

        // Categorías
        Categorias.Clear();
        Categorias.Add(new CategoriaChipItem { Nombre = "Todo", Emoji = "🍽️", IsSelected = CategoriaSeleccionada == "Todo" });
        foreach (var c in categorias)
            Categorias.Add(new CategoriaChipItem { Nombre = c.Nombre, Emoji = c.Emoji, IsSelected = c.Nombre == CategoriaSeleccionada });

        // Productos
        Productos.Clear();
        foreach (var p in productos) Productos.Add(p);
        FiltrarProductos();

        IsLoading = false;
    }

    // ── Filtro por categoría ──────────────────────────────────────────────────
    [RelayCommand]
    private void SeleccionarCategoria(string categoria)
    {
        CategoriaSeleccionada = categoria;
        foreach (var c in Categorias) c.IsSelected = c.Nombre == categoria;
        FiltrarProductos();
    }

    private void FiltrarProductos()
    {
        ProductosFiltrados.Clear();
        var filtrados = Productos.AsEnumerable();

        if (CategoriaSeleccionada != "Todo")
            filtrados = filtrados.Where(p => p.CategoriaNombre == CategoriaSeleccionada);

        if (!string.IsNullOrWhiteSpace(Busqueda))
            filtrados = filtrados.Where(p =>
                p.Nombre.Contains(Busqueda, StringComparison.OrdinalIgnoreCase));

        foreach (var p in filtrados) ProductosFiltrados.Add(p);
    }

    partial void OnBusquedaChanged(string value) => FiltrarProductos();

    // ── Navegación ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task IrAlCarritoAsync()
    {
        await Shell.Current.GoToAsync("//Main/Carrito");
    }

    [RelayCommand]
    private async Task VerDetalleProductoAsync(ProductoDto producto)
    {
        if (producto.Stock == 0) return; // producto agotado — no se puede abrir
        await Shell.Current.GoToAsync($"ProductoDetalle?productoId={producto.Id}");
    }

    // ── Añadir al carrito ─────────────────────────────────────────────────────
    [RelayCommand]
    private async Task AnadirAlCarritoAsync(ProductoDto producto)
    {
        if (!PuedePedir)
        {
            await Shell.Current.DisplayAlert(
                "Pedidos no disponibles",
                MensajeHorario,
                "OK");
            return;
        }

        if (producto.NivelStock == "agotado")
        {
            await Shell.Current.DisplayAlert(
                "Producto agotado",
                $"'{producto.Nombre}' no tiene stock disponible.",
                "OK");
            return;
        }

        _carrito.AnadirProducto(producto);

        // Toast de confirmación — try-catch por COMException en Windows unpackaged
        try
        {
            var toast = Toast.Make($"✓ {producto.Nombre} añadido", ToastDuration.Short, 14);
            await toast.Show();
        }
        catch { /* Toast no disponible en esta plataforma/configuración */ }
    }
}

public partial class CategoriaChipItem : ObservableObject
{
    public string Nombre { get; set; } = string.Empty;
    public string Emoji  { get; set; } = string.Empty;

    [ObservableProperty] private bool _isSelected;
}

// Helper para esperar múltiples Tasks en paralelo con tipos distintos
file static class TaskExtensions
{
    public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(
        this (Task<T1> t1, Task<T2> t2, Task<T3> t3) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2, tasks.t3);
        return (tasks.t1.Result, tasks.t2.Result, tasks.t3.Result);
    }
}
