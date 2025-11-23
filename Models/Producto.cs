// Models/Producto.cs
namespace CafeteriaInsti.Models
{
    public class Producto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public string UrlImagen { get; set; } = "https://via.placeholder.com/150";
        public string Categoria { get; set; } = "Bocadillos";
        public bool Disponible { get; set; } = true;
    }
}