// Services/ProductoService.cs
using CafeteriaInsti.Models;

namespace CafeteriaInsti.Services
{
    public class ProductoService
    {
        private List<Producto> _productos = new List<Producto>();

        public ProductoService()
        {
            // Cargamos los productos al crear el servicio
            CargarProductos();
        }

        public List<Producto> GetProductos()
        {
            return _productos;
        }

        private void CargarProductos()
        {
            // Aquí irían los productos de la cafetería del insti
            _productos = new List<Producto>
            {
                // Bebidas Calientes
                new Producto { Id = 1, Nombre = "Café Americano", Descripcion = "Café negro recién preparado", Precio = 1.50m, UrlImagen = "https://i.ibb.co/02sP6fC/zumo.png", Categoria = "Bebidas Calientes" },
                new Producto { Id = 2, Nombre = "Café con Leche", Descripcion = "Café con leche cremosa", Precio = 1.80m, UrlImagen = "https://i.ibb.co/02sP6fC/zumo.png", Categoria = "Bebidas Calientes" },
                new Producto { Id = 3, Nombre = "Chocolate Caliente", Descripcion = "Chocolate con leche y nata", Precio = 2.00m, UrlImagen = "https://i.ibb.co/02sP6fC/zumo.png", Categoria = "Bebidas Calientes" },
                
                // Bebidas Frías
                new Producto { Id = 4, Nombre = "Refresco Cola", Descripcion = "Lata de refresco de cola 33cl", Precio = 1.20m, UrlImagen = "https://i.ibb.co/3p8h8yF/cola.png", Categoria = "Bebidas Frías" },
                new Producto { Id = 5, Nombre = "Agua Mineral", Descripcion = "Botella de agua 50cl", Precio = 1.00m, UrlImagen = "https://i.ibb.co/h9t0q4Z/agua.png", Categoria = "Bebidas Frías" },
                new Producto { Id = 6, Nombre = "Zumo de Naranja", Descripcion = "Zumo de naranja natural recién exprimido", Precio = 2.00m, UrlImagen = "https://i.ibb.co/02sP6fC/zumo.png", Categoria = "Bebidas Frías" },
                
                // Postres
                new Producto { Id = 7, Nombre = "Napolitana de Chocolate", Descripcion = "Hojaldre con crema de chocolate", Precio = 1.80m, UrlImagen = "https://i.ibb.co/6yF1qK4/napolitana.png", Categoria = "Postres" },
                new Producto { Id = 8, Nombre = "Croissant", Descripcion = "Croissant de mantequilla", Precio = 1.50m, UrlImagen = "https://i.ibb.co/wYqT8pF/croissant.png", Categoria = "Postres" },
                new Producto { Id = 9, Nombre = "Tarta de Manzana", Descripcion = "Deliciosa tarta casera de manzana", Precio = 2.50m, UrlImagen = "https://i.ibb.co/6yF1qK4/napolitana.png", Categoria = "Postres" },
                
                // Snacks
                new Producto { Id = 10, Nombre = "Patatas Fritas", Descripcion = "Paquete de patatas fritas 40g", Precio = 1.10m, UrlImagen = "https://i.ibb.co/3s7hK5v/patatas.png", Categoria = "Snacks" },
                new Producto { Id = 11, Nombre = "Bocadillo de Jamón", Descripcion = "Jamón serrano con tomate fresco", Precio = 3.50m, UrlImagen = "https://i.ibb.co/68v8wLQ/bocadillo.png", Categoria = "Snacks" },
                new Producto { Id = 12, Nombre = "Bocadillo Vegetal", Descripcion = "Verduras asadas con queso crema", Precio = 3.25m, UrlImagen = "https://i.ibb.co/68v8wLQ/bocadillo.png", Categoria = "Snacks", Disponible = false }
            };
        }
    }
}