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

        [ObservableProperty]
        private ObservableCollection<Producto> _productos;

        public ListaProductosViewModel(ProductoService productoService)
        {
            Title = "Menú de la Cafetería";
            _productoService = productoService;

            // ✅ Inicializar la colección
            _productos = new ObservableCollection<Producto>();

            // ✅ CARGAR PRODUCTOS INMEDIATAMENTE
            CargarProductosInicial();
        }

        // ✅ NUEVO: Método privado para cargar en el constructor
        private void CargarProductosInicial()
        {
            try
            {
                var productos = _productoService.GetProductos();

                // Debug: Verificar cuántos productos hay
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Productos cargados: {productos?.Count ?? 0}");

                if (productos != null && productos.Any())
                {
                    foreach (var producto in productos)
                    {
                        Productos.Add(producto);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Añadido: {producto.Nombre}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ERROR] No se cargaron productos");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Exception: {ex.Message}");
            }
        }

        // ✅ Este comando es para el RefreshView
        [RelayCommand]
        private void LoadProductos()
        {
            IsBusy = true;
            try
            {
                var productos = _productoService.GetProductos();
                Productos.Clear();

                if (productos != null)
                {
                    foreach (var producto in productos)
                    {
                        Productos.Add(producto);
                    }
                }
            }
            finally
            {
                IsBusy = false;
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
                await Shell.Current.DisplayAlert("Error", $"No se pudo navegar: {ex.Message}", "OK");
            }
        }
    }
}