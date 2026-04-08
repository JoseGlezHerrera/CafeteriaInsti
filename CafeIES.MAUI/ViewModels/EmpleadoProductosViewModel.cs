using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class EmpleadoProductosViewModel : ObservableObject
{
    private readonly ApiService _api;

    public EmpleadoProductosViewModel(ApiService api) => _api = api;

    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<ProductoDto> Productos { get; } = new();

    [RelayCommand]
    public async Task CargarAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var productos = await _api.GetProductosAdminAsync();
            Productos.Clear();
            foreach (var p in productos) Productos.Add(p);
        }
        finally
        {
            IsLoading = false;  // BUG-038
        }
    }

    [RelayCommand]
    private async Task IrCategoriasAsync()
        => await Shell.Current.GoToAsync("AdminCategorias");

    [RelayCommand]
    private async Task IrIngredientesAsync()
        => await Shell.Current.GoToAsync("AdminIngredientes");

    [RelayCommand]
    private async Task NuevoProductoAsync()
        => await Shell.Current.GoToAsync("AdminEditProducto?productoId=0");

    [RelayCommand]
    private async Task ToggleActivoAsync(ProductoDto producto)
    {
        await _api.ToggleActivoAsync(producto.Id);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task EditarStockAsync(ProductoDto producto)
    {
        var result = await Shell.Current.DisplayPromptAsync(
            $"Stock — {producto.Nombre}",
            "Introduce el nuevo stock (-1 = ilimitado):",
            initialValue: producto.Stock.ToString(),
            keyboard: Keyboard.Numeric);

        if (result is null) return;
        if (!int.TryParse(result, out var nuevoStock) || nuevoStock < -1)
        {
            await Shell.Current.DisplayAlert("Error", "Valor no válido. Usa -1 para ilimitado o un número ≥ 0.", "OK");
            return;
        }

        await _api.ActualizarStockAsync(producto.Id, nuevoStock);
        await CargarAsync();
    }
}
