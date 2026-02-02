// Services/FavoritosService.cs
using System.Collections.ObjectModel;
using System.Text.Json;

namespace CafeteriaInsti.Services
{
    public class FavoritosService
    {
        private const string FAVORITOS_KEY = "favoritos_productos";
        private readonly HashSet<int> _favoritosIds = new();
        public event EventHandler<int>? FavoritoToggled; // Evento para notificar cambios

        public FavoritosService()
        {
            CargarFavoritos();
        }

        public bool IsFavorito(int productoId)
        {
            return _favoritosIds.Contains(productoId);
        }

        public void ToggleFavorito(int productoId)
        {
            if (_favoritosIds.Contains(productoId))
            {
                _favoritosIds.Remove(productoId);
                System.Diagnostics.Debug.WriteLine($"[INFO] Producto {productoId} eliminado de favoritos");
            }
            else
            {
                _favoritosIds.Add(productoId);
                System.Diagnostics.Debug.WriteLine($"[INFO] Producto {productoId} añadido a favoritos");
            }
            
            GuardarFavoritos();
            FavoritoToggled?.Invoke(this, productoId);
        }

        public IEnumerable<int> GetFavoritosIds()
        {
            return _favoritosIds;
        }

        public int CantidadFavoritos => _favoritosIds.Count;

        // ? NUEVA: Persistencia de favoritos
        private void GuardarFavoritos()
        {
            try
            {
                var json = JsonSerializer.Serialize(_favoritosIds.ToList());
                Preferences.Set(FAVORITOS_KEY, json);
                System.Diagnostics.Debug.WriteLine($"[INFO] Favoritos guardados: {_favoritosIds.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Guardar favoritos: {ex.Message}");
            }
        }

        // ? NUEVA: Cargar favoritos guardados
        private void CargarFavoritos()
        {
            try
            {
                var json = Preferences.Get(FAVORITOS_KEY, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    var favoritos = JsonSerializer.Deserialize<List<int>>(json);
                    if (favoritos != null)
                    {
                        _favoritosIds.Clear();
                        foreach (var id in favoritos)
                        {
                            _favoritosIds.Add(id);
                        }
                        System.Diagnostics.Debug.WriteLine($"[INFO] Favoritos cargados: {_favoritosIds.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Cargar favoritos: {ex.Message}");
            }
        }

        public void LimpiarFavoritos()
        {
            _favoritosIds.Clear();
            Preferences.Remove(FAVORITOS_KEY);
        }
    }
}

