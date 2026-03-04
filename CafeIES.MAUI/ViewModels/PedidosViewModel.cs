using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

// ── PedidosViewModel ─────────────────────────────────────────────────────────

public partial class PedidosViewModel : ObservableObject
{
    private readonly ApiService _api;

    public PedidosViewModel(ApiService api) => _api = api;

    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<PedidoDto> Pedidos { get; } = new();

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsLoading = true;
        var pedidos = await _api.GetMisPedidosAsync();
        Pedidos.Clear();
        foreach (var p in pedidos) Pedidos.Add(p);
        IsLoading = false;
    }

    [RelayCommand]
    private async Task VerDetallePedidoAsync(PedidoDto pedido)
    {
        await Shell.Current.GoToAsync($"DetallePedido?pedidoId={pedido.Id}");
    }
}


// ── PerfilViewModel ───────────────────────────────────────────────────────────

public partial class PerfilViewModel : ObservableObject
{
    private readonly ApiService   _api;
    private readonly TokenService _tokens;

    public PerfilViewModel(ApiService api, TokenService tokens)
    {
        _api    = api;
        _tokens = tokens;
    }

    [ObservableProperty] private string _nombreCompleto  = string.Empty;
    [ObservableProperty] private string _email           = string.Empty;
    [ObservableProperty] private string _rolTexto        = string.Empty;
    [ObservableProperty] private string _turnoTexto      = string.Empty;
    [ObservableProperty] private bool   _tieneTurno;
    [ObservableProperty] private string _resumenHorario  = string.Empty;
    [ObservableProperty] private int    _totalPedidos;
    [ObservableProperty] private decimal _totalGastado;

    [RelayCommand]
    public async Task CargarAsync()
    {
        var usuario = await _tokens.GetUsuarioAsync();
        if (usuario is not null)
        {
            NombreCompleto = usuario.NombreCompleto;
            Email          = usuario.Email;
            RolTexto = usuario.Rol switch
            {
                RolUsuario.Alumno   => "🎓 Alumno",
                RolUsuario.Profesor => "👨‍🏫 Profesor",
                RolUsuario.Personal => "🏢 Personal",
                RolUsuario.Admin    => "⚙️ Admin",
                _                  => "Usuario"
            };
            TieneTurno = usuario.Turno.HasValue;
            TurnoTexto = usuario.Turno switch
            {
                Turno.Manana => "☀️ Mañana",
                Turno.Tarde  => "🌤️ Tarde",
                Turno.Noche  => "🌙 Noche",
                _            => string.Empty
            };
        }

        var status = await _api.GetHorarioStatusAsync();
        ResumenHorario = status?.Mensaje ?? "Sin información";

        var pedidos = await _api.GetMisPedidosAsync();
        TotalPedidos = pedidos.Count;
        TotalGastado = pedidos.Sum(p => p.Total);
    }

    [RelayCommand]
    private async Task CerrarSesionAsync()
    {
        var confirmar = await Shell.Current.DisplayAlert(
            "Cerrar sesión", "¿Seguro que quieres salir?", "Sí", "Cancelar");

        if (!confirmar) return;

        _tokens.LimpiarTokens();
        await Shell.Current.GoToAsync("//Login");
    }
}
