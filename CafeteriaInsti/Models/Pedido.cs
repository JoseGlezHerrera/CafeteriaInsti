// Models/Pedido.cs
using System.Collections.Generic;
using System.Linq;

namespace CafeteriaInsti.Models
{
    public class Pedido
    {
        public string NumeroPedido { get; set; } = string.Empty;
        public DateTime FechaPedido { get; set; }
        public List<ItemCarrito> Items { get; set; } = new();
        
        // ? Calcular total dinámicamente desde la lista de items
        public decimal Total => Items?.Sum(i => i.Producto.Precio * i.Cantidad) ?? 0;
        
        // ? Calcular cantidad de items dinámicamente desde la lista
        public int CantidadItems => Items?.Sum(i => i.Cantidad) ?? 0;
    }
}



