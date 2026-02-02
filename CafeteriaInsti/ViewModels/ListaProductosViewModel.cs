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
        private readonly SessionService _sessionService;
        private List<Producto> _todosLosProductos;

        [ObservableProperty]
        private ObservableCollection<Producto> _productos;

        [ObservableProperty]
        private string _textoBusqueda = string.Empty;

        [ObservableProperty]
        private string _categoriaSeleccionada = "Todas";

        [ObservableProperty]
        private string _usuarioActual = string.Empty;

        // ✅ Método que se ejecuta automáticamente cuando cambia CategoriaSeleccionada
        partial void OnCategoriaSeleccionadaChanged(string value)
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

        public ListaProductosViewModel(ProductoService productoService, SessionService sessionService)
        {
            Title = "Menú de la Cafetería";
            _productoService = productoService;
            _sessionService = sessionService;
            _productos = new ObservableCollection<Producto>();
            _todosLosProductos = new List<Producto>();

            // Establecer nombre del usuario actual
            if (_sessionService.IsLoggedIn)
            {
                UsuarioActual = _sessionService.CurrentUser?.FullName ?? "Usuario";
            }

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
            System.Diagnostics.Debug.WriteLine($"[DEBUG] CategoriaSeleccionada: '{CategoriaSeleccionada}'");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] TextoBusqueda: '{TextoBusqueda}'");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Total productos: {_todosLosProductos.Count}");

            var productosFiltrados = _todosLosProductos.AsEnumerable();

            // Filtrar por categoría
            if (CategoriaSeleccionada != "Todas")
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Filtrando por categoría: {CategoriaSeleccionada}");
                
                // Mostrar todas las categorías disponibles
                foreach (var p in _todosLosProductos)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG]   Producto: {p.Nombre} | Categoría: '{p.Categoria}' | Coincide: {p.Categoria == CategoriaSeleccionada}");
                }
                
                productosFiltrados = productosFiltrados.Where(p => p.Categoria == CategoriaSeleccionada);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Productos después de filtrar por categoría: {productosFiltrados.Count()}");
            }

            // Filtrar por búsqueda
            if (!string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Filtrando por búsqueda: {TextoBusqueda}");
                productosFiltrados = productosFiltrados.Where(p =>
                    p.Nombre.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                    p.Descripcion.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase));
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Productos después de filtrar por búsqueda: {productosFiltrados.Count()}");
            }

            Productos.Clear();
            var listaFinal = productosFiltrados.ToList();
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Productos finales a mostrar: {listaFinal.Count}");
            
            foreach (var producto in listaFinal)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG]   Agregando: {producto.Nombre}");
                Productos.Add(producto);
            }
            
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
                System.Diagnostics.Debug.WriteLine($"[ERROR] Navegación falló: {ex.Message}");
                await Shell.Current.DisplayAlertAsync("Error", $"No se pudo navegar: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        private async Task Logout()
        {
            try
            {
                // Confirmar logout
                bool confirmado = await Application.Current.MainPage.DisplayAlert(
                    "Cerrar sesión",
                    $"¿Estás seguro de que quieres cerrar sesión, {UsuarioActual}?",
                    "Sí", "No");

                if (confirmado)
                {
                    // Limpiar sesión
                    _sessionService.ClearSession();

                    // Volver a LoginPage
                    if (Shell.Current is AppShell appShell)
                    {
                        await appShell.ShowLoginPage();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error en logout: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Error", "Error al cerrar sesión", "OK");
            }
        }

        [RelayCommand]
        private async Task EditProfile()
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(EditProfilePage), animate: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error al editar perfil: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Error", "Error al abrir edición de perfil", "OK");
            }
        }
    }
}
