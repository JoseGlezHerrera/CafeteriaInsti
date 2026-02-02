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
        
        // ✅ NUEVAS PROPIEDADES
        public decimal PrecioOriginal { get; set; } // Para ofertas
        public bool TieneDescuento => PrecioOriginal > Precio;
        public int PorcentajeDescuento => TieneDescuento ? (int)(((PrecioOriginal - Precio) / PrecioOriginal) * 100) : 0;
        public int TiempoPreparacionMinutos { get; set; } = 5; // Tiempo estimado de preparación
        public double Valoracion { get; set; } = 0; // 0-5 estrellas
        public int NumeroValoraciones { get; set; } = 0;
        public List<string> Ingredientes { get; set; } = new(); // Para búsqueda avanzada
        public List<string> Alergenos { get; set; } = new(); // Para filtrado
    }
}
