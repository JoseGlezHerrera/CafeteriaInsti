using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class AdminUsuariosViewModel : ObservableObject
{
    private readonly ApiService _api;

    public AdminUsuariosViewModel(ApiService api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hayPendientes;

    public ObservableCollection<UsuarioDto> Pendientes { get; } = new();
    public ObservableCollection<UsuarioDto> Todos      { get; } = new();

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsLoading = true;
        var lista = await _api.GetTodosUsuariosAsync();

        Pendientes.Clear();
        Todos.Clear();

        foreach (var u in lista.Where(u => u.Estado == EstadoCuenta.PendienteValidacion))
            Pendientes.Add(u);

        foreach (var u in lista.Where(u => u.Estado != EstadoCuenta.PendienteValidacion))
            Todos.Add(u);

        HayPendientes = Pendientes.Count > 0;
        IsLoading = false;
    }

    [RelayCommand]
    private async Task AprobarAsync(UsuarioDto usuario)
    {
        await _api.ValidarAlumnoAsync(usuario.Id, true);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task RechazarAsync(UsuarioDto usuario)
    {
        var ok = await Shell.Current.DisplayAlert(
            "Rechazar", $"¿Rechazar a {usuario.NombreCompleto}?", "Sí, rechazar", "Cancelar");
        if (!ok) return;
        await _api.ValidarAlumnoAsync(usuario.Id, false);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task SuspenderAsync(UsuarioDto usuario)
    {
        var ok = await Shell.Current.DisplayAlert(
            "Suspender", $"¿Suspender la cuenta de {usuario.NombreCompleto}?", "Sí", "Cancelar");
        if (!ok) return;
        await _api.SuspenderUsuarioAsync(usuario.Id);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task ReactivarAsync(UsuarioDto usuario)
    {
        await _api.ReactivarUsuarioAsync(usuario.Id);
        await CargarAsync();
    }
}
