// Models/ItemCarrito.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace CafeteriaInsti.Models
{
    public partial class ItemCarrito : ObservableObject
    {
        public Producto Producto { get; set; } = new Producto();

        [ObservableProperty]
        private int _cantidad;

        // ✅ NUEVO: Propiedad calculada para el subtotal
        public decimal Subtotal => Producto.Precio * Cantidad;

        // ✅ MEJORADO: Notificar cambios en Subtotal cuando cambia Cantidad
        partial void OnCantidadChanged(int value)
        {
            OnPropertyChanged(nameof(Subtotal));
        }
    }
}
