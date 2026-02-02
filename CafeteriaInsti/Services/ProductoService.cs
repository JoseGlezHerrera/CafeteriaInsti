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
                new Producto { 
                    Id = 1, 
                    Nombre = "Café Americano", 
                    Descripcion = "Café negro recién preparado", 
                    Precio = 1.50m, 
                    PrecioOriginal = 1.50m,
                    UrlImagen = "https://i.ibb.co/02sP6fC/zumo.png", 
                    Categoria = "Bebidas Calientes",
                    TiempoPreparacionMinutos = 3,
                    Valoracion = 4.5,
                    NumeroValoraciones = 120,
                    Ingredientes = new List<string> { "Café", "Agua" },
                    Alergenos = new List<string>()
                },
                new Producto { 
                    Id = 2, 
                    Nombre = "Café con Leche", 
                    Descripcion = "Café con leche cremosa", 
                    Precio = 1.80m,
                    PrecioOriginal = 1.80m,
                    UrlImagen = "https://i.ibb.co/02sP6fC/zumo.png", 
                    Categoria = "Bebidas Calientes",
                    TiempoPreparacionMinutos = 3,
                    Valoracion = 4.7,
                    NumeroValoraciones = 200,
                    Ingredientes = new List<string> { "Café", "Leche" },
                    Alergenos = new List<string> { "Lactosa" }
                },
                new Producto { 
                    Id = 3, 
                    Nombre = "Chocolate Caliente", 
                    Descripcion = "Chocolate con leche y nata", 
                    Precio = 1.60m,
                    PrecioOriginal = 2.00m, // ¡OFERTA!
                    UrlImagen = "https://i.ibb.co/02sP6fC/zumo.png", 
                    Categoria = "Bebidas Calientes",
                    TiempoPreparacionMinutos = 4,
                    Valoracion = 4.8,
                    NumeroValoraciones = 150,
                    Ingredientes = new List<string> { "Chocolate", "Leche", "Nata" },
                    Alergenos = new List<string> { "Lactosa" }
                },
                
                // Bebidas Frias
                new Producto { 
                    Id = 4, 
                    Nombre = "Refresco Cola", 
                    Descripcion = "Lata de refresco de cola 33cl", 
                    Precio = 1.20m,
                    PrecioOriginal = 1.20m,
                    UrlImagen = "https://i.ibb.co/3p8h8yF/cola.png", 
                    Categoria = "Bebidas Frias",
                    TiempoPreparacionMinutos = 1,
                    Valoracion = 4.0,
                    NumeroValoraciones = 85,
                    Ingredientes = new List<string> { "Agua", "Azúcar", "Cafeína" },
                    Alergenos = new List<string>()
                },
                new Producto { 
                    Id = 5, 
                    Nombre = "Agua Mineral", 
                    Descripcion = "Botella de agua 50cl", 
                    Precio = 1.00m,
                    PrecioOriginal = 1.00m,
                    UrlImagen = "https://i.ibb.co/h9t0q4Z/agua.png", 
                    Categoria = "Bebidas Frias",
                    TiempoPreparacionMinutos = 1,
                    Valoracion = 4.2,
                    NumeroValoraciones = 60,
                    Ingredientes = new List<string> { "Agua" },
                    Alergenos = new List<string>()
                },
                new Producto { 
                    Id = 6, 
                    Nombre = "Zumo de Naranja", 
                    Descripcion = "Zumo de naranja natural recien exprimido", 
                    Precio = 2.00m,
                    PrecioOriginal = 2.00m,
                    UrlImagen = "https://i.ibb.co/02sP6fC/zumo.png", 
                    Categoria = "Bebidas Frias",
                    TiempoPreparacionMinutos = 5,
                    Valoracion = 4.9,
                    NumeroValoraciones = 180,
                    Ingredientes = new List<string> { "Naranjas frescas" },
                    Alergenos = new List<string>()
                },
                
                // Postres
                new Producto { 
                    Id = 7, 
                    Nombre = "Napolitana de Chocolate", 
                    Descripcion = "Hojaldre con crema de chocolate", 
                    Precio = 1.80m,
                    PrecioOriginal = 1.80m,
                    UrlImagen = "https://i.ibb.co/6yF1qK4/napolitana.png", 
                    Categoria = "Postres",
                    TiempoPreparacionMinutos = 2,
                    Valoracion = 4.6,
                    NumeroValoraciones = 95,
                    Ingredientes = new List<string> { "Harina", "Mantequilla", "Chocolate" },
                    Alergenos = new List<string> { "Gluten", "Lactosa" }
                },
                new Producto { 
                    Id = 8, 
                    Nombre = "Croissant", 
                    Descripcion = "Croissant de mantequilla", 
                    Precio = 1.20m,
                    PrecioOriginal = 1.50m, // ¡OFERTA!
                    UrlImagen = "https://i.ibb.co/wYqT8pF/croissant.png", 
                    Categoria = "Postres",
                    TiempoPreparacionMinutos = 2,
                    Valoracion = 4.4,
                    NumeroValoraciones = 110,
                    Ingredientes = new List<string> { "Harina", "Mantequilla", "Levadura" },
                    Alergenos = new List<string> { "Gluten", "Lactosa" }
                },
                new Producto { 
                    Id = 9, 
                    Nombre = "Tarta de Manzana", 
                    Descripcion = "Deliciosa tarta casera de manzana", 
                    Precio = 2.50m,
                    PrecioOriginal = 2.50m,
                    UrlImagen = "https://i.ibb.co/6yF1qK4/napolitana.png", 
                    Categoria = "Postres",
                    TiempoPreparacionMinutos = 3,
                    Valoracion = 4.7,
                    NumeroValoraciones = 75,
                    Ingredientes = new List<string> { "Manzana", "Harina", "Azúcar", "Canela" },
                    Alergenos = new List<string> { "Gluten" }
                },
                
                // Snacks
                new Producto { 
                    Id = 10, 
                    Nombre = "Patatas Fritas", 
                    Descripcion = "Paquete de patatas fritas 40g", 
                    Precio = 1.10m,
                    PrecioOriginal = 1.10m,
                    UrlImagen = "https://i.ibb.co/3s7hK5v/patatas.png", 
                    Categoria = "Snacks",
                    TiempoPreparacionMinutos = 1,
                    Valoracion = 4.1,
                    NumeroValoraciones = 55,
                    Ingredientes = new List<string> { "Patatas", "Aceite", "Sal" },
                    Alergenos = new List<string>()
                },
                new Producto { 
                    Id = 11, 
                    Nombre = "Bocadillo de Jamón", 
                    Descripcion = "Jamón serrano con tomate fresco", 
                    Precio = 3.50m,
                    PrecioOriginal = 3.50m,
                    UrlImagen = "https://i.ibb.co/68v8wLQ/bocadillo.png", 
                    Categoria = "Snacks",
                    TiempoPreparacionMinutos = 5,
                    Valoracion = 4.8,
                    NumeroValoraciones = 130,
                    Ingredientes = new List<string> { "Pan", "Jamón serrano", "Tomate", "Aceite" },
                    Alergenos = new List<string> { "Gluten" }
                },
                new Producto { 
                    Id = 12, 
                    Nombre = "Bocadillo Vegetal", 
                    Descripcion = "Verduras asadas con queso crema", 
                    Precio = 3.25m,
                    PrecioOriginal = 3.25m,
                    UrlImagen = "https://i.ibb.co/68v8wLQ/bocadillo.png", 
                    Categoria = "Snacks", 
                    Disponible = false,
                    TiempoPreparacionMinutos = 6,
                    Valoracion = 4.5,
                    NumeroValoraciones = 45,
                    Ingredientes = new List<string> { "Pan", "Pimientos", "Berenjena", "Calabacín", "Queso" },
                    Alergenos = new List<string> { "Gluten", "Lactosa" }
                }
            };
        }
    }
}