// Models/ItemCarrito.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace CafeteriaInsti.Models
{
    // ✅ CORREGIDO: Ahora hereda de ObservableObject
    public partial class ItemCarrito : ObservableObject
    {
        public Producto Producto { get; set; } = new Producto();

        // ✅ CORREGIDO: Ahora usa [ObservableProperty] para notificar cambios
        [ObservableProperty]
        private int _cantidad;
    }
}