using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Collections.ObjectModel;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModoEdicion))]
    [NotifyPropertyChangedFor(nameof(TextoBotonCarrito))]
    private int _productoId;
    [ObservableProperty] private bool        _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneProducto))]
    [NotifyPropertyChangedFor(nameof(TieneDescripcion))]
    [NotifyPropertyChangedFor(nameof(AlergenosTexto))]
    [NotifyPropertyChangedFor(nameof(PuedeAnadir))]
    [NotifyPropertyChangedFor(nameof(ImagenUrlCompleta))]
    [NotifyPropertyChangedFor(nameof(TieneImagen))]
    [NotifyPropertyChangedFor(nameof(TieneIngredientes))]
    [NotifyPropertyChangedFor(nameof(PrecioConPersonalizacion))]
    private ProductoDto? _producto;

    public bool   TieneProducto    => Producto is not null;
    public bool   TieneDescripcion => !string.IsNullOrWhiteSpace(Producto?.Descripcion);
    public bool   PuedeAnadir     => Producto is not null && Producto.NivelStock != "agotado";
    public string ImagenUrlCompleta => Producto?.ImagenUrl is { Length: > 0 } url
        ? _api.BuildImageUrl(url) : string.Empty;
    public bool   TieneImagen     => !string.IsNullOrEmpty(ImagenUrlCompleta);

    public string AlergenosTexto =>
        Producto?.Alergenos.Count > 0
            ? string.Join("  ", Producto.Alergenos.Select(a => $"{a.Emoji} {a.Nombre}"))
            : string.Empty;

    // ── Ingredientes personalizables ──────────────────────────────────────────

    public ObservableCollection<IngredienteSeleccionVm> IngredientesSeleccion { get; } = new();
    public bool    TieneIngredientes       => IngredientesSeleccion.Count > 0;
    public decimal PrecioExtra             => IngredientesSeleccion.Sum(i => i.PrecioExtraActivo);
    public decimal PrecioConPersonalizacion => (Producto?.Precio ?? 0) + PrecioExtra;

    partial void OnProductoChanged(ProductoDto? value)
    {
        // Desuscribir items anteriores
        foreach (var vm in IngredientesSeleccion)
            vm.PropertyChanged -= OnIngredienteChanged;
        IngredientesSeleccion.Clear();

        if (value?.Ingredientes is null || value.Ingredientes.Count == 0)
        {
            OnPropertyChanged(nameof(TieneIngredientes));
            return;
        }

        foreach (var ing in value.Ingredientes)
        {
            var vm = new IngredienteSeleccionVm
            {
                Config       = ing,
                Seleccionado = ing.EsBase   // base ingredients start checked
            };
            vm.PropertyChanged += OnIngredienteChanged;
            IngredientesSeleccion.Add(vm);
        }

        // Pre-fill selections when arriving from the cart in edit mode
        if (ModoEdicion && _carrito.ItemCarritoEnEdicion is not null)
        {
            foreach (var ir in _carrito.ItemCarritoEnEdicion.Ingredientes)
            {
                var vm = IngredientesSeleccion.FirstOrDefault(v => v.Config.IngredienteId == ir.IngredienteId);
                if (vm is null) continue;
                if (ir.Accion == AccionIngrediente.Quitar)
                    vm.Seleccionado = false;
                else
                {
                    if (vm.UsaStepper)
                        vm.Cantidad = Math.Max(1, ir.Cantidad);
                    else
                        vm.Seleccionado = true;
                }
            }
        }

        OnPropertyChanged(nameof(TieneIngredientes));
        OnPropertyChanged(nameof(PrecioExtra));
        OnPropertyChanged(nameof(PrecioConPersonalizacion));
    }

    private void OnIngredienteChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IngredienteSeleccionVm.Seleccionado) or
                              nameof(IngredienteSeleccionVm.Cantidad))
        {
            OnPropertyChanged(nameof(PrecioExtra));
            OnPropertyChanged(nameof(PrecioConPersonalizacion));
        }
    }

    // ── Modo edición (cuando se llega desde el carrito) ───────────────────────

    /// <summary>true cuando el usuario llega desde el carrito para editar ingredientes de un item existente.</summary>
    public bool ModoEdicion =>
        _carrito.ItemCarritoEnEdicion?.ProductoId == ProductoId && ProductoId > 0;

    public string TextoBotonCarrito =>
        ModoEdicion ? "Actualizar carrito" : "Añadir al carrito";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    partial void OnProductoIdChanged(int value)
    {
        if (value > 0) _ = CargarAsync();
    }

    [RelayCommand]
    public async Task CargarAsync()
    {
        if (ProductoId <= 0) return;
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            Producto = await _api.GetProductoByIdAsync(ProductoId);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Añadir al carrito ─────────────────────────────────────────────────────

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

        // Construir la lista de modificaciones (solo las que difieren del estado por defecto)
        bool EsModificado(IngredienteSeleccionVm vm) =>
            (vm.Config.EsBase && !vm.Seleccionado) ||
            (!vm.Config.EsBase && (vm.UsaStepper ? vm.Cantidad > 0 : vm.Seleccionado));

        var ingredientesRequest = IngredientesSeleccion
            .Where(EsModificado)
            .Select(vm => new IngredienteRequest(
                vm.Config.IngredienteId,
                vm.Config.EsBase ? AccionIngrediente.Quitar : AccionIngrediente.Añadir,
                vm.Config.EsBase ? 1 : (vm.UsaStepper ? vm.Cantidad : 1)))
            .ToList();

        var precioExtra = IngredientesSeleccion.Sum(i => i.PrecioExtraActivo);

        var descripcion = string.Join(", ", IngredientesSeleccion
            .Where(EsModificado)
            .Select(vm =>
            {
                if (vm.Config.EsBase) return $"sin {vm.Config.Nombre}";
                var cnt = vm.UsaStepper ? vm.Cantidad : 1;
                return cnt > 1 ? $"×{cnt} {vm.Config.Nombre}" : $"+ {vm.Config.Nombre}";
            }));

        // ── Modo edición: reemplazar item existente en el carrito ─────────────
        if (ModoEdicion && _carrito.ItemCarritoEnEdicion is not null)
        {
            _carrito.ReemplazarIngredientesItem(
                _carrito.ItemCarritoEnEdicion, ingredientesRequest, precioExtra, descripcion);
            await Shell.Current.GoToAsync("..");
            return;
        }

        bool añadido = _carrito.AnadirProducto(Producto, ingredientesRequest, precioExtra, descripcion);

        if (!añadido)
        {
            await Shell.Current.DisplayAlert(
                "Límite alcanzado",
                $"Ya tienes el máximo de 20 unidades de '{Producto.Nombre}'.",
                "OK");
            return;
        }

        try
        {
            var toast = Toast.Make($"✓ {Producto.Nombre} añadido", ToastDuration.Short, 14);
            await toast.Show();
        }
        catch { /* Toast no disponible en esta plataforma */ }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task VolverAsync()
        => await Shell.Current.GoToAsync("..");
}

// ── ViewModel por ingrediente personalizable ──────────────────────────────────

public partial class IngredienteSeleccionVm : ObservableObject
{
    public required ProductoIngredienteDto Config { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrecioExtraActivo))]
    [NotifyPropertyChangedFor(nameof(EtiquetaPrecio))]
    private bool _seleccionado;

    /// <summary>Cantidad seleccionada (solo relevante cuando UsaStepper=true).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrecioExtraActivo))]
    [NotifyPropertyChangedFor(nameof(EtiquetaPrecio))]
    private int _cantidad = 1;

    /// <summary>Máximo de unidades configurado en el producto (heredado de Config).</summary>
    public int CantidadMaxima => Config.CantidadMaxima;

    /// <summary>true cuando este extra admite más de una unidad (muestra stepper en lugar de switch).</summary>
    public bool UsaStepper => !Config.EsBase && Config.CantidadMaxima > 1;

    /// <summary>Se puede modificar si es base+quitable o si es un extra.</summary>
    public bool PuedeModificar =>
        (Config.EsBase && Config.EsQuitable) || !Config.EsBase;

    /// <summary>Precio extra activo: para steppers multiplica por Cantidad; para switches, binario.</summary>
    public decimal PrecioExtraActivo =>
        Config.EsBase ? 0 :
        UsaStepper ? Config.PrecioExtra * Cantidad :
        Seleccionado ? Config.PrecioExtra : 0;

    /// <summary>Texto de precio extra para mostrar en la UI.</summary>
    public string EtiquetaPrecio
    {
        get
        {
            if (Config.PrecioExtra <= 0) return string.Empty;
            if (UsaStepper && Cantidad > 1) return $"+{Config.PrecioExtra * Cantidad:F2}€ (×{Cantidad})";
            return $"+{Config.PrecioExtra:F2}€";
        }
    }

    [RelayCommand]
    private void IncrementarCantidad()
    {
        if (Cantidad < CantidadMaxima) Cantidad++;
    }

    [RelayCommand]
    private void DecrementarCantidad()
    {
        if (Cantidad > 0) Cantidad--;
    }
}
