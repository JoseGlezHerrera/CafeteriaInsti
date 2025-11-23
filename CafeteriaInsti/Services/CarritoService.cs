// Services/CarritoService.cs
using CafeteriaInsti.Models;
using System.Collections.ObjectModel;

namespace CafeteriaInsti.Services
{
    public class CarritoService
    {
        // Usamos una colección observable para que la UI se actualice automáticamente
        public ObservableCollection<ItemCarrito> Items { get; }

        public CarritoService()
        {
            Items = new ObservableCollection<ItemCarrito>();
        }

        public void AnadirAlCarrito(Producto producto)
        {
            var itemExistente = Items.FirstOrDefault(i => i.Producto.Id == producto.Id);

            if (itemExistente != null)
            {
                // Si el producto ya está en el carrito, aumentamos la cantidad
                itemExistente.Cantidad++;
            }
            else
            {
                // Si no, lo añadimos como un nuevo item
                Items.Add(new ItemCarrito { Producto = producto, Cantidad = 1 });
            }
        }

        public void EliminarDelCarrito(int productoId)
        {
            var item = Items.FirstOrDefault(i => i.Producto.Id == productoId);
            if (item != null)
            {
                Items.Remove(item);
            }
        }

        public void ActualizarCantidad(int productoId, int cantidad)
        {
            if (cantidad <= 0)
            {
                EliminarDelCarrito(productoId);
                return;
            }

            var item = Items.FirstOrDefault(i => i.Producto.Id == productoId);
            if (item != null)
            {
                item.Cantidad = cantidad;
            }
        }

        public decimal GetTotal()
        {
            return Items.Sum(item => item.Producto.Precio * item.Cantidad);
        }

        public void LimpiarCarrito()
        {
            Items.Clear();
        }
    }
}