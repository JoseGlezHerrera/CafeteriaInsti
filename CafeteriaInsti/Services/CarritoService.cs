// Services/CarritoService.cs
using CafeteriaInsti.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace CafeteriaInsti.Services
{
    public class CarritoService
    {
        private const string CARRITO_KEY = "carrito_guardado";
        
        // Usamos una colección observable para que la UI se actualice automáticamente
        public ObservableCollection<ItemCarrito> Items { get; }

        public CarritoService()
        {
            Items = new ObservableCollection<ItemCarrito>();
            CargarCarritoGuardado();
        }

        public void AnadirAlCarrito(Producto producto)
        {
            System.Diagnostics.Debug.WriteLine($"[INFO] AnadirAlCarrito - Producto: {producto.Nombre}");
            
            var itemExistente = Items.FirstOrDefault(i => i.Producto.Id == producto.Id);

            if (itemExistente != null)
            {
                // Si el producto ya está en el carrito, aumentamos la cantidad
                itemExistente.Cantidad++;
                System.Diagnostics.Debug.WriteLine($"[INFO] Producto existente - Nueva cantidad: {itemExistente.Cantidad}");
            }
            else
            {
                // Si no, lo añadimos como un nuevo item
                Items.Add(new ItemCarrito { Producto = producto, Cantidad = 1 });
                System.Diagnostics.Debug.WriteLine($"[INFO] Producto nuevo agregado - Total items: {Items.Count}");
            }
            
            GuardarCarrito();
        }

        public void EliminarDelCarrito(int productoId)
        {
            var item = Items.FirstOrDefault(i => i.Producto.Id == productoId);
            if (item != null)
            {
                Items.Remove(item);
                GuardarCarrito();
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
                GuardarCarrito();
            }
        }

        public decimal GetTotal()
        {
            return Items.Sum(item => item.Producto.Precio * item.Cantidad);
        }

        public int GetTiempoPreparacionTotal()
        {
            // Calcular tiempo total basado en los productos
            return Items.Sum(item => item.Producto.TiempoPreparacionMinutos * item.Cantidad);
        }

        public void LimpiarCarrito()
        {
            System.Diagnostics.Debug.WriteLine($"[INFO] LimpiarCarrito llamado - Items antes: {Items.Count}");
            Items.Clear();
            GuardarCarrito();
            System.Diagnostics.Debug.WriteLine($"[INFO] LimpiarCarrito completado - Items después: {Items.Count}");
        }

        // ✅ NUEVA: Persistencia del carrito
        private void GuardarCarrito()
        {
            try
            {
                var json = JsonSerializer.Serialize(Items.ToList());
                Preferences.Set(CARRITO_KEY, json);
                System.Diagnostics.Debug.WriteLine($"[INFO] Carrito guardado - Items: {Items.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Guardar carrito: {ex.Message}");
            }
        }

        // ✅ NUEVA: Cargar carrito guardado
        private void CargarCarritoGuardado()
        {
            try
            {
                var json = Preferences.Get(CARRITO_KEY, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    var items = JsonSerializer.Deserialize<List<ItemCarrito>>(json);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            Items.Add(item);
                        }
                        System.Diagnostics.Debug.WriteLine($"[INFO] Carrito cargado - Items: {Items.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Cargar carrito: {ex.Message}");
            }
        }
    }
}
