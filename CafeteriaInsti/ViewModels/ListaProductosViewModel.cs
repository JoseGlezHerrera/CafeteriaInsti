// ViewModels/ListaProductosViewModel.cs
using CafeteriaInsti.Models;
using CafeteriaInsti.Services;
using CafeteriaInsti.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CafeteriaInsti.ViewModels
{
    public partial class ListaProductosViewModel : BaseViewModel
    {
        private readonly ProductoService _productoService;
        private readonly FavoritosService _favoritosService;
        private List<Producto> _todosLosProductos;

        [ObservableProperty]
        private ObservableCollection<Producto> _productos;

        [ObservableProperty]
        private string _textoBusqueda = string.Empty;

        [ObservableProperty]
        private string _categoriaSeleccionada = "Todas";

        [ObservableProperty]
        private decimal _precioMinimo = 0;

        [ObservableProperty]
        private decimal _precioMaximo = 100;

        [ObservableProperty]
        private bool _soloOfertas = false;

        [ObservableProperty]
        private bool _soloDisponibles = false;

        [ObservableProperty]
        private string _ordenSeleccionado = "Ninguno";

        [ObservableProperty]
        private bool _mostrarFiltrosAvanzados = false;

        public List<string> Categorias { get; } = new List<string>
        {
            "Todas",
            "Bebidas Calientes",
            "Bebidas Frias",
            "Postres",
            "Snacks"
        };

        public List<string> OpcionesOrden { get; } = new List<string>
        {
            "Ninguno",
            "Precio Ascendente",
            "Precio Descendente",
            "Nombre A-Z",
            "Nombre Z-A"
        };

        public ListaProductosViewModel(ProductoService productoService, FavoritosService favoritosService)
        {
            Title = "Menu de la Cafeteria";
            _productoService = productoService;
            _favoritosService = favoritosService;
            _productos = new ObservableCollection<Producto>();
            _todosLosProductos = new List<Producto>();
            
            System.Diagnostics.Debug.WriteLine("[CONSTRUCTOR] ListaProductosViewModel creado");
            
            // Cargar productos inmediatamente
            CargarProductos();
            
            System.Diagnostics.Debug.WriteLine($"[CONSTRUCTOR] Productos cargados: {Productos.Count}");
        }

        private void CargarProductos()
        {
            System.Diagnostics.Debug.WriteLine("[CargarProductos] INICIO");
            
            try
            {
                var productos = _productoService.GetProductos();
                System.Diagnostics.Debug.WriteLine($"[CargarProductos] Productos del servicio: {productos?.Count ?? 0}");

                if (productos != null && productos.Any())
                {
                    _todosLosProductos = productos.ToList();
                    System.Diagnostics.Debug.WriteLine($"[CargarProductos] _todosLosProductos tiene: {_todosLosProductos.Count}");
                    
                    // Limpiar y agregar todos los productos
                    Productos.Clear();
                    System.Diagnostics.Debug.WriteLine($"[CargarProductos] Productos.Clear() ejecutado");
                    
                    foreach (var producto in productos)
                    {
                        Productos.Add(producto);
                        System.Diagnostics.Debug.WriteLine($"[CargarProductos] Agregado: {producto.Nombre} (ID: {producto.Id})");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[CargarProductos] Total productos en Productos collection: {Productos.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] ProductoService.GetProductos() devolvió NULL o vacío!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Exception en CargarProductos: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
            }
            
            System.Diagnostics.Debug.WriteLine("[CargarProductos] FIN");
        }

        [RelayCommand]
        private void Buscar()
        {
            AplicarFiltros();
        }

        [RelayCommand]
        private void SeleccionarCategoria(string categoria)
        {
            if (!string.IsNullOrEmpty(categoria))
            {
                CategoriaSeleccionada = categoria;
                System.Diagnostics.Debug.WriteLine($"[INFO] Categoria seleccionada: {categoria}");
                AplicarFiltros();
            }
        }

        private void AplicarFiltros()
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] === AplicarFiltros ===");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Categoria: {CategoriaSeleccionada}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Busqueda: {TextoBusqueda}");

            var productosFiltrados = _todosLosProductos.AsEnumerable();

            // Filtrar por categoría
            if (CategoriaSeleccionada != "Todas")
            {
                productosFiltrados = productosFiltrados.Where(p => p.Categoria == CategoriaSeleccionada);
            }

            // Filtrar por búsqueda
            if (!string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                productosFiltrados = productosFiltrados.Where(p =>
                    p.Nombre.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                    p.Descripcion.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                    (p.Ingredientes?.Any(i => i.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                    (p.Alergenos?.Any(a => a.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase)) ?? false));
            }

            // Filtrar por precio
            productosFiltrados = productosFiltrados.Where(p => p.Precio >= _precioMinimo && p.Precio <= _precioMaximo);

            // Filtrar solo ofertas
            if (_soloOfertas)
            {
                productosFiltrados = productosFiltrados.Where(p => p.TieneDescuento);
            }

            // Filtrar solo disponibles
            if (_soloDisponibles)
            {
                productosFiltrados = productosFiltrados.Where(p => p.Disponible);
            }

            // Ordenar
            productosFiltrados = _ordenSeleccionado switch
            {
                "Precio Ascendente" => productosFiltrados.OrderBy(p => p.Precio),
                "Precio Descendente" => productosFiltrados.OrderByDescending(p => p.Precio),
                "Nombre A-Z" => productosFiltrados.OrderBy(p => p.Nombre),
                "Nombre Z-A" => productosFiltrados.OrderByDescending(p => p.Nombre),
                _ => productosFiltrados
            };

            Productos.Clear();
            foreach (var producto in productosFiltrados)
            {
                Productos.Add(producto);
            }
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Productos filtrados: {Productos.Count}");
        }

        [RelayCommand]
        private async Task GoToDetailsAsync(Producto producto)
        {
            if (producto == null) return;

            try
            {
                await Shell.Current.GoToAsync(nameof(DetalleProductoPage), new Dictionary<string, object>
                {
                    {"Producto", producto}
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Navegacion fallo: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleFavorito(Producto producto)
        {
            if (producto == null) return;
            _favoritosService.ToggleFavorito(producto.Id);
            System.Diagnostics.Debug.WriteLine($"[INFO] Favorito toggled: {producto.Nombre}");
        }

        [RelayCommand]
        private void RestablecerFiltros()
        {
            TextoBusqueda = string.Empty;
            CategoriaSeleccionada = "Todas";
            PrecioMinimo = 0;
            PrecioMaximo = 100;
            SoloOfertas = false;
            SoloDisponibles = false;
            OrdenSeleccionado = "Ninguno";
            
            // Recargar todos los productos
            Productos.Clear();
            foreach (var producto in _todosLosProductos)
            {
                Productos.Add(producto);
            }
        }

        [RelayCommand]
        private void ToggleFiltrosAvanzados()
        {
            MostrarFiltrosAvanzados = !MostrarFiltrosAvanzados;
        }
    }
}
