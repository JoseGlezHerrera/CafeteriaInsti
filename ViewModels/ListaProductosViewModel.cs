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

        // Volvemos a la propiedad manual para evitar cualquier rareza con [ObservableProperty]
        private ObservableCollection<Producto> _productos;
        public ObservableCollection<Producto> Productos
        {
            get => _productos;
            set
            {
                _productos = value;
                OnPropertyChanged();
            }
        }

        public ListaProductosViewModel(ProductoService productoService)
        {
            Title = "Menú de la Cafetería";
            _productoService = productoService;
            _productos = new ObservableCollection<Producto>();

            // <-- CAMBIO CLAVE: Cargamos los datos aquí directamente
            LoadProductos();
        }

        [RelayCommand]
        private void LoadProductos()
        {
            IsBusy = true;
            try
            {
                var productos = _productoService.GetProductos();
                Productos.Clear();
                foreach (var producto in productos)
                {
                    Productos.Add(producto);
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
            await Shell.Current.GoToAsync(nameof(DetalleProductoPage), new Dictionary<string, object>
            {
                {"Producto", producto}
            });
        }
    }
}