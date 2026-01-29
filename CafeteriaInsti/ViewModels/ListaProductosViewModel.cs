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
        private List<Producto> _todosLosProductos;

        [ObservableProperty]
        private ObservableCollection<Producto> _productos;

        [ObservableProperty]
        private string _textoBusqueda = string.Empty;

        [ObservableProperty]
        private string _categoriaSeleccionada = "Todas";

        public List<string> Categorias { get; } = new List<string>
        {
            "Todas",
            "Bebidas Calientes",
            "Bebidas Frías",
            "Postres",
            "Snacks"
        };

        public ListaProductosViewModel(ProductoService productoService)
        {
            Title = "Menú de la Cafetería";
            _productoService = productoService;
            _productos = new ObservableCollection<Producto>();
            _todosLosProductos = new List<Producto>();
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

        private void AplicarFiltros()
        {
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
                    p.Descripcion.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase));
            }

            Productos.Clear();
            foreach (var producto in productosFiltrados)
            {
                Productos.Add(producto);
            }
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
                System.Diagnostics.Debug.WriteLine($"[ERROR] Navegación falló: {ex.Message}");
                await Shell.Current.DisplayAlertAsync("Error", $"No se pudo navegar: {ex.Message}", "OK");
            }
        }
    }
}
