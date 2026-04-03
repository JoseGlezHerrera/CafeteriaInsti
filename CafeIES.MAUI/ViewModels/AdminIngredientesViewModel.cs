using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class AdminIngredientesViewModel : ObservableObject
{
    private readonly ApiService _api;

    public AdminIngredientesViewModel(ApiService api) => _api = api;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _error = string.Empty;

    public ObservableCollection<IngredienteDto> Ingredientes { get; } = new();

    [RelayCommand]
    private async Task VolverAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    public async Task CargarAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        HasError  = false;
        try
        {
            var lista = await _api.GetIngredientesAdminAsync();
            Ingredientes.Clear();
            foreach (var i in lista) Ingredientes.Add(i);
        }
        catch
        {
            Error    = "Error al cargar los ingredientes.";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NuevoIngredienteAsync()
    {
        var nombre = await Shell.Current.DisplayPromptAsync(
            "Nuevo ingrediente", "Nombre", "OK", "Cancelar", "ej. Cebolla caramelizada", maxLength: 80);
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var emojiStr = await Shell.Current.DisplayPromptAsync(
            "Emoji", "Emoji del ingrediente (opcional)", "OK", "Cancelar", "🧅", maxLength: 4);
        if (emojiStr is null) return;

        var precioStr = await Shell.Current.DisplayPromptAsync(
            "Precio extra (€)", "0 = gratis, ej: 0.50", "OK", "Cancelar", "0", keyboard: Keyboard.Numeric);
        if (precioStr is null) return;
        decimal.TryParse(precioStr.Replace(',', '.'),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out decimal precio);

        var stockStr = await Shell.Current.DisplayPromptAsync(
            "Stock", "-1 = ilimitado", "OK", "Cancelar", "-1", keyboard: Keyboard.Numeric);
        if (stockStr is null) return;
        int.TryParse(stockStr, out int stock);
        if (stock < -1) stock = -1;

        var req = new CrearIngredienteRequest(nombre.Trim(), emojiStr.Trim(), precio, stock);
        var ok  = await _api.CrearIngredienteAsync(req);

        if (ok)
            await CargarAsync();
        else
            await Shell.Current.DisplayAlert("Error", "No se pudo crear el ingrediente.", "OK");
    }

    [RelayCommand]
    private async Task EditarAsync(IngredienteDto ing)
    {
        var nombre = await Shell.Current.DisplayPromptAsync(
            "Editar ingrediente", "Nombre", "OK", "Cancelar",
            initialValue: ing.Nombre, maxLength: 80);
        if (nombre is null) return;
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var emojiStr = await Shell.Current.DisplayPromptAsync(
            "Emoji", "Emoji del ingrediente", "OK", "Cancelar",
            initialValue: ing.Emoji, maxLength: 4);
        if (emojiStr is null) return;

        var precioStr = await Shell.Current.DisplayPromptAsync(
            "Precio extra (€)", "0 = gratis", "OK", "Cancelar",
            initialValue: ing.PrecioExtra.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            keyboard: Keyboard.Numeric);
        if (precioStr is null) return;
        decimal.TryParse(precioStr.Replace(',', '.'),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out decimal precio);

        var stockStr = await Shell.Current.DisplayPromptAsync(
            "Stock", "-1 = ilimitado", "OK", "Cancelar",
            initialValue: ing.Stock.ToString(), keyboard: Keyboard.Numeric);
        if (stockStr is null) return;
        int.TryParse(stockStr, out int stock);
        if (stock < -1) stock = -1;

        var req = new CrearIngredienteRequest(nombre.Trim(), emojiStr.Trim(), precio, stock);
        var ok  = await _api.ActualizarIngredienteAsync(ing.Id, req);

        if (ok)
            await CargarAsync();
        else
            await Shell.Current.DisplayAlert("Error", "No se pudo actualizar el ingrediente.", "OK");
    }

    [RelayCommand]
    private async Task ToggleActivoAsync(IngredienteDto ing)
    {
        await _api.ToggleActivoIngredienteAsync(ing.Id);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task EliminarAsync(IngredienteDto ing)
    {
        var confirmar = await Shell.Current.DisplayAlert(
            "Eliminar ingrediente",
            $"¿Eliminar '{ing.Emoji} {ing.Nombre}'?",
            "Sí, eliminar", "Cancelar");
        if (!confirmar) return;

        var (ok, error) = await _api.EliminarIngredienteAsync(ing.Id);

        if (ok)
            await CargarAsync();
        else
            await Shell.Current.DisplayAlert("No se puede eliminar", error ?? "Error al eliminar.", "OK");
    }
}
