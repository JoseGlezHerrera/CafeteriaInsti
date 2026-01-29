// Services/FavoritosService.cs
using System.Collections.ObjectModel;

namespace CafeteriaInsti.Services
{
    public class FavoritosService
    {
        private readonly HashSet<int> _favoritosIds = new();

        public bool IsFavorito(int productoId)
        {
            return _favoritosIds.Contains(productoId);
        }

        public void ToggleFavorito(int productoId)
        {
            if (_favoritosIds.Contains(productoId))
            {
                _favoritosIds.Remove(productoId);
            }
            else
            {
                _favoritosIds.Add(productoId);
            }
        }

        public IEnumerable<int> GetFavoritosIds()
        {
            return _favoritosIds;
        }
    }
}
