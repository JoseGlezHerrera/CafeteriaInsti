using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class AdminCategoriasViewModel : ObservableObject
{
    private readonly ApiService _api;

    public AdminCategoriasViewModel(ApiService api) => _api = api;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _error = string.Empty;
    public bool HasError => !string.IsNullOrEmpty(Error);

    public ObservableCollection<CategoriaDto> Categorias { get; } = new();

    [RelayCommand]
    private async Task VolverAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    public async Task CargarAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        Error = string.Empty;
        try
        {
            var lista = await _api.GetCategoriasAsync();
            Categorias.Clear();
            foreach (var c in lista) Categorias.Add(c);
        }
        catch
        {
            Error = "Error al cargar las categorías.";
            OnPropertyChanged(nameof(HasError));
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task NuevaCategoriaAsync()
    {
        var nombre = await Shell.Current.DisplayPromptAsync(
            "Nueva categoría", "Nombre", "OK", "Cancelar",
            "ej. Bocadillos", maxLength: 60);
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var emoji = await Shell.Current.DisplayPromptAsync(
            "Emoji", "Emoji de la categoría", "OK", "Cancelar",
            "🥪", maxLength: 4);
        if (emoji is null) return;

        var ok = await _api.CrearCategoriaAsync(nombre.Trim(), emoji.Trim());
        if (ok)
            await CargarAsync();
        else
            await Shell.Current.DisplayAlert("Error", "No se pudo crear la categoría.", "OK");
    }

    public async Task EliminarAsync(CategoriaDto cat)
    {
        var confirmar = await Shell.Current.DisplayAlert(
            "Eliminar categoría",
            $"¿Eliminar '{cat.Emoji} {cat.Nombre}'? Los productos de esta categoría quedarán sin categoría asignada.",
            "Sí, eliminar", "Cancelar");
        if (!confirmar) return;

        var ok = await _api.EliminarCategoriaAsync(cat.Id);
        if (ok)
            await CargarAsync();
        else
            await Shell.Current.DisplayAlert("Error", "No se pudo eliminar la categoría.", "OK");
    }
}
