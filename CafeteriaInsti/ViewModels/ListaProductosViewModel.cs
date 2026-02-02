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

        // ✅ Método que se ejecuta automáticamente cuando cambia CategoriaSeleccionada
        partial void OnCategoriaSeleccionadaChanged(string value)
        {
            AplicarFiltros();
        }

        partial void OnSoloOfertasChanged(bool value)
        {
            AplicarFiltros();
        }

        partial void OnSoloDisponiblesChanged(bool value)
        {
            AplicarFiltros();
        }

        partial void OnOrdenSeleccionadoChanged(string value)
        {
            AplicarFiltros();
        }

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
            
            // Suscribirse a cambios en favoritos para refrescar UI
            _favoritosService.FavoritoToggled += (s, e) =>
            {
                // Forzar actualización de la UI
                var temp = Productos.ToList();
                Productos.Clear();
                foreach (var p in temp)
                {
                    Productos.Add(p);
                }
            };
            
            CargarProductosInicial();
        }

        private void CargarProductosInicial()
        {
            try
            {
                var productos = _productoService.GetProductos();

                if (productos != null && productos.Any())
                {
                    _todosLosProductos = productos.ToList();
                    foreach (var producto in productos)
                    {
                        Productos.Add(producto);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Exception: {ex.Message}");
            }
        }

        [RelayCommand]
        private void LoadProductos()
        {
            IsBusy = true;
            try
            {
                var productos = _productoService.GetProductos();
                _todosLosProductos = productos?.ToList() ?? new List<Producto>();
                AplicarFiltros();
            }
            finally
            {
                IsBusy = false;
            }
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
                System.Diagnostics.Debug.WriteLine($"[INFO] Categoría seleccionada: {categoria}");
            }
        }

        private void AplicarFiltros()
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] === AplicarFiltros INICIO ===");

            var productosFiltrados = _todosLosProductos.AsEnumerable();

            // Filtrar por categoría
            if (CategoriaSeleccionada != "Todas")
            {
                productosFiltrados = productosFiltrados.Where(p => p.Categoria == CategoriaSeleccionada);
            }

            // Filtrar por búsqueda (nombre, descripción, ingredientes)
            if (!string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                productosFiltrados = productosFiltrados.Where(p =>
                    p.Nombre.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                    p.Descripcion.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                    p.Ingredientes.Any(i => i.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase)) ||
                    p.Alergenos.Any(a => a.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase)));
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
            System.Diagnostics.Debug.WriteLine($"[DEBUG] === AplicarFiltros FIN ===");
        }

        [RelayCommand]
        private async Task GoToDetailsAsync(Producto producto)
        {
            if (producto == null)
                return;

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
                await Shell.Current.DisplayAlertAsync("Error", $"No se pudo navegar: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        private void ToggleFavorito(Producto producto)
        {
            if (producto == null) return;
            
            _favoritosService.ToggleFavorito(producto.Id);
            
            // Forzar actualización de la UI
            OnPropertyChanged(nameof(Productos));
            
            System.Diagnostics.Debug.WriteLine($"[INFO] Favorito toggled para: {producto.Nombre}");
        }

        // ✅ NUEVO: Verificar si es favorito
        public bool IsFavorito(int productoId)
        {
            return _favoritosService.IsFavorito(productoId);
        }

        // ✅ NUEVO: Aplicar filtros de precio
        [RelayCommand]
        private void AplicarFiltroPrecio()
        {
            AplicarFiltros();
        }

        // ✅ NUEVO: Restablecer filtros
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
            AplicarFiltros();
        }

        // ✅ NUEVO: Toggle filtros avanzados
        [RelayCommand]
        private void ToggleFiltrosAvanzados()
        {
            MostrarFiltrosAvanzados = !MostrarFiltrosAvanzados;
        }
    }
}
