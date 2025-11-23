// Models/ItemCarrito.cs
using CafeteriaInsti.Models;

namespace CafeteriaInsti.Models;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class ItemCarrito : ObservableObject
{
    public Producto Producto { get; set; } = new Producto();

    [ObservableProperty]
    private int _cantidad; // ✅ Ahora notifica cambios
}