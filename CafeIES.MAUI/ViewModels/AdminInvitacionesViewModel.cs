using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class AdminInvitacionesViewModel : ObservableObject
{
    private readonly ApiService _api;

    public AdminInvitacionesViewModel(ApiService api) => _api = api;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _error = string.Empty;

    public ObservableCollection<InvitacionDto> Invitaciones { get; } = new();

    [RelayCommand]
    private async Task VolverAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsLoading = true;
        HasError  = false;

        var lista = await _api.GetInvitacionesAsync();
        Invitaciones.Clear();
        foreach (var inv in lista) Invitaciones.Add(inv);

        IsLoading = false;
    }

    [RelayCommand]
    private async Task NuevaInvitacionAsync()
    {
        var tipoStr = await Shell.Current.DisplayActionSheet(
            "Tipo de invitación", "Cancelar", null, "Profesor", "Personal", "Empleado");
        if (string.IsNullOrEmpty(tipoStr) || tipoStr == "Cancelar") return;

        if (!Enum.TryParse<TipoInvitacion>(tipoStr, out var tipo)) return;

        var diasStr = await Shell.Current.DisplayPromptAsync(
            "Días de validez", "¿Cuántos días será válida?", "OK", "Cancelar",
            initialValue: "7", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(diasStr)) return;
        if (!int.TryParse(diasStr, out var dias) || dias <= 0) dias = 7;

        var usosStr = await Shell.Current.DisplayPromptAsync(
            "Usos máximos", "Número de usos permitidos (vacío = ilimitado)",
            "OK", "Cancelar", keyboard: Keyboard.Numeric);
        int? usosMaximos = null;
        if (!string.IsNullOrWhiteSpace(usosStr) && int.TryParse(usosStr, out var usos) && usos > 0)
            usosMaximos = usos;

        var req = new CrearInvitacionRequest(tipo, usosMaximos, dias);
        var ok  = await _api.CrearInvitacionAsync(req);

        if (ok)
            await CargarAsync();
        else
        {
            Error    = "Error al crear la invitación.";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task RevocarInvitacionAsync(InvitacionDto inv)
    {
        var confirmar = await Shell.Current.DisplayAlert(
            "Revocar invitación",
            $"¿Revocar la invitación de tipo '{inv.Tipo}'? No podrá usarse más.",
            "Sí, revocar", "Cancelar");
        if (!confirmar) return;

        var ok = await _api.EliminarInvitacionAsync(inv.Id);

        if (ok)
            await CargarAsync();
        else
        {
            Error    = "Error al revocar la invitación.";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task CopiarUrlAsync(InvitacionDto inv)
    {
        try
        {
            await Clipboard.Default.SetTextAsync(inv.Token);
            await Shell.Current.DisplayAlert("Token copiado",
                $"Token copiado:\n{inv.Token}\n\nEl nuevo usuario debe introducirlo en el campo 'Código de invitación' al registrarse.", "OK");
        }
        catch
        {
            Error    = "No se pudo copiar al portapapeles.";
            HasError = true;
        }
    }
}
