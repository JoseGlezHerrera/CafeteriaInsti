using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class AdminAlergenosViewModel : ObservableObject
{
    private readonly ApiService _api;

    public AdminAlergenosViewModel(ApiService api) => _api = api;

    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<AlergenoDto> Alergenos { get; } = new();

    [RelayCommand]
    private async Task VolverAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    public async Task CargarAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var lista = await _api.GetAlergenosAsync();
            Alergenos.Clear();
            foreach (var a in lista) Alergenos.Add(a);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task NuevoAlergenoAsync()
    {
        var nombre = await Shell.Current.DisplayPromptAsync(
            "Nuevo alérgeno", "Nombre", "OK", "Cancelar",
            "ej. Gluten", maxLength: 60);
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var emoji = await Shell.Current.DisplayPromptAsync(
            "Emoji", "Emoji del alérgeno (opcional)", "OK", "Cancelar",
            "🌾", maxLength: 4);
        if (emoji is null) return;

        var ok = await _api.CrearAlergenoAsync(nombre.Trim(), emoji.Trim());
        if (ok)
            await CargarAsync();
        else
            await Shell.Current.DisplayAlert("Error", "No se pudo crear el alérgeno.", "OK");
    }

    public async Task EliminarAsync(AlergenoDto alergeno)
    {
        var confirmar = await Shell.Current.DisplayAlert(
            "Eliminar alérgeno",
            $"¿Eliminar '{alergeno.Emoji} {alergeno.Nombre}'? Se quitará de todos los productos que lo tengan asignado.",
            "Sí, eliminar", "Cancelar");
        if (!confirmar) return;

        var ok = await _api.EliminarAlergenoAsync(alergeno.Id);
        if (ok)
            await CargarAsync();
        else
            await Shell.Current.DisplayAlert("Error", "No se pudo eliminar el alérgeno.", "OK");
    }
}
