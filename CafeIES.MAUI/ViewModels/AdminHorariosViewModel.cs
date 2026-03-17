using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class AdminHorariosViewModel : ObservableObject
{
    private readonly ApiService _api;

    public AdminHorariosViewModel(ApiService api) => _api = api;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _error = string.Empty;

    public ObservableCollection<FranjaHorariaDto> Franjas { get; } = new();

    [RelayCommand]
    private async Task VolverAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsLoading = true;
        HasError  = false;

        var franjas = await _api.GetHorariosAsync();
        Franjas.Clear();
        foreach (var f in franjas) Franjas.Add(f);

        IsLoading = false;
    }

    [RelayCommand]
    private async Task NuevaFranjaAsync()
    {
        // Pedir descripción
        var descripcion = await Shell.Current.DisplayPromptAsync(
            "Nueva franja", "Descripción", "OK", "Cancelar", "Ej. Recreo mañana");
        if (string.IsNullOrWhiteSpace(descripcion)) return;

        var horaInicio = await Shell.Current.DisplayPromptAsync(
            "Hora inicio", "Formato HH:mm", "OK", "Cancelar", "08:00");
        if (string.IsNullOrWhiteSpace(horaInicio)) return;

        var horaFin = await Shell.Current.DisplayPromptAsync(
            "Hora fin", "Formato HH:mm", "OK", "Cancelar", "08:30");
        if (string.IsNullOrWhiteSpace(horaFin)) return;

        var turnoStr = await Shell.Current.DisplayActionSheet(
            "Turno", "Cancelar", null, "Manana", "Tarde", "Noche");
        if (string.IsNullOrEmpty(turnoStr) || turnoStr == "Cancelar") return;

        if (!Enum.TryParse<Turno>(turnoStr, out var turno)) turno = Turno.Manana;

        var req = new UpsertFranjaRequest(turno, descripcion, horaInicio, horaFin, true);
        var ok  = await _api.CrearFranjaAsync(req);

        if (ok)
            await CargarAsync();
        else
        {
            Error    = "Error al crear la franja horaria.";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task EditarFranjaAsync(FranjaHorariaDto franja)
    {
        var descripcion = await Shell.Current.DisplayPromptAsync(
            "Editar franja", "Descripción", "OK", "Cancelar",
            initialValue: franja.Descripcion);
        if (descripcion is null) return;

        var horaInicio = await Shell.Current.DisplayPromptAsync(
            "Hora inicio", "Formato HH:mm", "OK", "Cancelar",
            initialValue: franja.HoraInicio);
        if (horaInicio is null) return;

        var horaFin = await Shell.Current.DisplayPromptAsync(
            "Hora fin", "Formato HH:mm", "OK", "Cancelar",
            initialValue: franja.HoraFin);
        if (horaFin is null) return;

        var req = new UpsertFranjaRequest(franja.Turno, descripcion, horaInicio, horaFin, franja.Activa);
        var ok  = await _api.ActualizarFranjaAsync(franja.Id, req);

        if (ok)
            await CargarAsync();
        else
        {
            Error    = "Error al actualizar la franja horaria.";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task EliminarFranjaAsync(FranjaHorariaDto franja)
    {
        var confirmar = await Shell.Current.DisplayAlert(
            "Eliminar franja",
            $"¿Eliminar la franja '{franja.Descripcion}' ({franja.HoraInicio}–{franja.HoraFin})?",
            "Sí, eliminar", "Cancelar");
        if (!confirmar) return;

        var ok = await _api.EliminarFranjaAsync(franja.Id);

        if (ok)
            await CargarAsync();
        else
        {
            Error    = "Error al eliminar la franja horaria.";
            HasError = true;
        }
    }
}
