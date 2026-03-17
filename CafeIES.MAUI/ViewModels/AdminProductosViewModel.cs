using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

// ── AdminProductosViewModel ───────────────────────────────────────────────────

public partial class AdminProductosViewModel : ObservableObject
{
    private readonly ApiService _api;

    public AdminProductosViewModel(ApiService api) => _api = api;

    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<ProductoDto> Productos { get; } = new();

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsLoading = true;
        var productos = await _api.GetProductosAdminAsync();
        Productos.Clear();
        foreach (var p in productos) Productos.Add(p);
        IsLoading = false;
    }

    [RelayCommand]
    private async Task NuevoProductoAsync()
        => await Shell.Current.GoToAsync("AdminEditProducto?productoId=0");

    [RelayCommand]
    private async Task EditarAsync(ProductoDto producto)
        => await Shell.Current.GoToAsync($"AdminEditProducto?productoId={producto.Id}");

    [RelayCommand]
    private async Task ToggleActivoAsync(ProductoDto producto)
    {
        await _api.ToggleActivoAsync(producto.Id);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task EliminarAsync(ProductoDto producto)
    {
        var ok = await Shell.Current.DisplayAlert(
            "Desactivar producto",
            $"¿Desactivar '{producto.Nombre}'? Podrás reactivarlo después.",
            "Desactivar", "Cancelar");
        if (!ok) return;
        await _api.EliminarProductoAsync(producto.Id);
        await CargarAsync();
    }
}

// ── AdminEditProductoViewModel ────────────────────────────────────────────────

/// <summary>Alérgeno con estado de selección para el formulario de producto.</summary>
public partial class AlergenoSeleccionable : ObservableObject
{
    public AlergenoDto Alergeno { get; init; } = null!;
    [ObservableProperty] private bool _seleccionado;
}

public partial class AdminEditProductoViewModel : ObservableObject, IQueryAttributable
{
    private readonly ApiService _api;

    public AdminEditProductoViewModel(ApiService api) => _api = api;

    [ObservableProperty] private int     _productoId;
    [ObservableProperty] private bool    _isLoading;
    [ObservableProperty] private bool    _isSaving;
    [ObservableProperty] private string  _titulo       = "Nuevo producto";
    [ObservableProperty] private string  _nombre       = string.Empty;
    [ObservableProperty] private string  _descripcion  = string.Empty;
    [ObservableProperty] private decimal _precio;
    [ObservableProperty] private int     _stock        = -1;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _error = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(Error);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneImagen))]
    private string? _imagenUrl;

    public bool TieneImagen => !string.IsNullOrEmpty(ImagenUrl);

    [ObservableProperty] private bool _isSubiendoImagen;

    [ObservableProperty]
    private CategoriaDto? _categoriaSeleccionada;

    public ObservableCollection<CategoriaDto> Categorias { get; } = new();
    public ObservableCollection<AlergenoSeleccionable> Alergenos { get; } = new();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("productoId", out var val) &&
            int.TryParse(val?.ToString(), out int id))
            ProductoId = id;
    }

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsLoading = true;
        Error = string.Empty;

        var cats = await _api.GetCategoriasAsync();
        Categorias.Clear();
        foreach (var c in cats) Categorias.Add(c);

        var alergenos = await _api.GetAlergenosAsync();

        if (ProductoId > 0)
        {
            Titulo = "Editar producto";
            var prod = await _api.GetProductoByIdAsync(ProductoId);
            if (prod is not null)
            {
                Nombre       = prod.Nombre;
                Descripcion  = prod.Descripcion ?? string.Empty;
                Precio       = prod.Precio;
                Stock        = prod.Stock;
                ImagenUrl    = prod.ImagenUrl is not null
                    ? _api.BuildImageUrl(prod.ImagenUrl)
                    : null;
                CategoriaSeleccionada = Categorias.FirstOrDefault(c => c.Id == prod.CategoriaId);

                Alergenos.Clear();
                foreach (var a in alergenos)
                    Alergenos.Add(new AlergenoSeleccionable
                    {
                        Alergeno = a,
                        Seleccionado = prod.Alergenos.Any(pa => pa.Id == a.Id)
                    });
            }
        }
        else
        {
            Titulo = "Nuevo producto";
            Nombre       = string.Empty;
            Descripcion  = string.Empty;
            Precio       = 0;
            Stock        = -1;
            ImagenUrl    = null;
            CategoriaSeleccionada = Categorias.FirstOrDefault();

            Alergenos.Clear();
            foreach (var a in alergenos)
                Alergenos.Add(new AlergenoSeleccionable { Alergeno = a, Seleccionado = false });
        }

        IsLoading = false;
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            Error = "El nombre es obligatorio.";
            return;
        }
        if (CategoriaSeleccionada is null)
        {
            Error = "Selecciona una categoría.";
            return;
        }

        IsSaving = true;
        Error    = string.Empty;

        var alergenoIds = Alergenos
            .Where(a => a.Seleccionado)
            .Select(a => a.Alergeno.Id)
            .ToList();

        var req = new CrearProductoRequest(
            Nombre.Trim(), Descripcion.Trim(), Precio, Stock, CategoriaSeleccionada.Id, null, alergenoIds);

        bool ok = ProductoId > 0
            ? await _api.ActualizarProductoAsync(ProductoId, req)
            : await _api.CrearProductoAsync(req);

        IsSaving = false;

        if (!ok) { Error = "Error al guardar. Inténtalo de nuevo."; return; }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task SeleccionarImagenAsync()
    {
        if (ProductoId <= 0)
        {
            Error = "Guarda el producto primero antes de subir una imagen.";
            return;
        }

        try
        {
            var foto = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Seleccionar imagen del producto"
            });
            if (foto is null) return;

            IsSubiendoImagen = true;
            Error = string.Empty;

            using var stream = await foto.OpenReadAsync();
            var ext          = Path.GetExtension(foto.FileName).ToLowerInvariant();
            var contentType  = ext is ".png" ? "image/png"
                             : ext is ".webp" ? "image/webp"
                             : "image/jpeg";

            var url = await _api.SubirImagenProductoAsync(ProductoId, stream, foto.FileName, contentType);

            if (url is null)
                Error = "Error al subir la imagen. Comprueba el formato y el tamaño (máx. 5 MB).";
            else
                ImagenUrl = _api.BuildImageUrl(url);
        }
        catch (Exception ex) when (ex is PermissionException || ex is FeatureNotSupportedException)
        {
            Error = "No se pudo acceder a la galería. Comprueba los permisos de la app.";
        }
        finally
        {
            IsSubiendoImagen = false;
        }
    }

    [RelayCommand]
    private async Task CancelarAsync() => await Shell.Current.GoToAsync("..");
}
