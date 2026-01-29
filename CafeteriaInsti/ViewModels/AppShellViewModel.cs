// ViewModels/AppShellViewModel.cs
using CafeteriaInsti.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CafeteriaInsti.ViewModels
{
    public partial class AppShellViewModel : ObservableObject
    {
        private readonly CarritoService _carritoService;

        [ObservableProperty]
        private int _cantidadItemsCarrito;

        public AppShellViewModel(CarritoService carritoService)
        {
            _carritoService = carritoService;
            _carritoService.Items.CollectionChanged += (s, e) => ActualizarCantidadItems();
            ActualizarCantidadItems();
        }

        private void ActualizarCantidadItems()
        {
            CantidadItemsCarrito = _carritoService.Items.Sum(i => i.Cantidad);
        }
    }
}
