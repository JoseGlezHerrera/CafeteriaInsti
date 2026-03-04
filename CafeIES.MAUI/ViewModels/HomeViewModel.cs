using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly CarritoViewModel _carrito;

    public HomeViewModel(ApiService api, CarritoViewModel carrito)
    {
        _api = api;
        _carrito = carrito;

        // Sincronizar badge con el estado real del carrito (singleton)
        ItemsEnCarrito = _carrito.TotalItems;
        _carrito.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CarritoViewModel.TotalItems))
                ItemsEnCarrito = _carrito.TotalItems;
        };
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

    public ObservableCollection<CategoriaDto>  Categorias  { get; } = new();
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
        IsLoading = true;

        var (horario, categorias, productos) = await (
            _api.GetHorarioStatusAsync(),
            _api.GetCategoriasAsync(),
            _api.GetProductosAsync()
        ).WhenAll();

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
        foreach (var c in categorias) Categorias.Add(c);

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

    partial void OnBusquedaChanged(string _) => FiltrarProductos();

    // ── Navegación ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task IrAlCarritoAsync()
    {
        await Shell.Current.GoToAsync("//Main/Carrito");
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

        _carrito.AnadirProducto(producto);
        // ItemsEnCarrito se actualiza por la suscripción a PropertyChanged
    }
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
