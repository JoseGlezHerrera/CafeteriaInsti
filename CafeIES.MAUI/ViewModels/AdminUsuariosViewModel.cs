using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeIES.Shared.Models;
using CafeIES.MAUI.Services;
using System.Collections.ObjectModel;

namespace CafeIES.MAUI.ViewModels;

public partial class AdminUsuariosViewModel : ObservableObject
{
    private readonly ApiService _api;
    private List<UsuarioDto> _todos = new();

    public AdminUsuariosViewModel(ApiService api) => _api = api;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hayPendientes;
    [ObservableProperty] private string _textoBusqueda = string.Empty;

    // Filtros activos
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FiltroRolTodosActivo))]
    [NotifyPropertyChangedFor(nameof(FiltroRolAlumnoActivo))]
    [NotifyPropertyChangedFor(nameof(FiltroRolProfesorActivo))]
    [NotifyPropertyChangedFor(nameof(FiltroRolPersonalActivo))]
    private string _filtroRol = "Todos";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FiltroEstadoTodosActivo))]
    [NotifyPropertyChangedFor(nameof(FiltroEstadoActivoActivo))]
    [NotifyPropertyChangedFor(nameof(FiltroEstadoPendienteActivo))]
    [NotifyPropertyChangedFor(nameof(FiltroEstadoSuspendidoActivo))]
    private string _filtroEstado = "Todos";

    public bool FiltroRolTodosActivo     => FiltroRol == "Todos";
    public bool FiltroRolAlumnoActivo    => FiltroRol == "Alumno";
    public bool FiltroRolProfesorActivo  => FiltroRol == "Profesor";
    public bool FiltroRolPersonalActivo  => FiltroRol == "Personal";

    public bool FiltroEstadoTodosActivo      => FiltroEstado == "Todos";
    public bool FiltroEstadoActivoActivo     => FiltroEstado == "Activa";
    public bool FiltroEstadoPendienteActivo  => FiltroEstado == "Pendiente";
    public bool FiltroEstadoSuspendidoActivo => FiltroEstado == "Suspendida";

    public ObservableCollection<UsuarioDto> Pendientes { get; } = new();
    public ObservableCollection<UsuarioDto> Todos      { get; } = new();

    partial void OnTextoBusquedaChanged(string value) => AplicarFiltros();
    partial void OnFiltroRolChanged(string value)     => AplicarFiltros();
    partial void OnFiltroEstadoChanged(string value)  => AplicarFiltros();

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsLoading = true;
        _todos = await _api.GetTodosUsuariosAsync();
        AplicarFiltros();
        IsLoading = false;
    }

    private void AplicarFiltros()
    {
        var query = _todos.AsEnumerable();

        // Pendientes siempre se muestran aparte sin filtros de rol/estado
        Pendientes.Clear();
        foreach (var u in _todos.Where(u => u.Estado == EstadoCuenta.PendienteValidacion))
            Pendientes.Add(u);
        HayPendientes = Pendientes.Count > 0;

        // Excluir pendientes de la lista principal
        query = query.Where(u => u.Estado != EstadoCuenta.PendienteValidacion);

        // Filtro rol
        if (FiltroRol != "Todos")
        {
            if (Enum.TryParse<RolUsuario>(FiltroRol, out var rol))
                query = query.Where(u => u.Rol == rol);
        }

        // Filtro estado
        query = FiltroEstado switch
        {
            "Activa"     => query.Where(u => u.Estado == EstadoCuenta.Activa),
            "Suspendida" => query.Where(u => u.Estado == EstadoCuenta.Suspendida),
            _            => query
        };

        // Búsqueda por nombre o email
        if (!string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            var t = TextoBusqueda.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.NombreCompleto.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        Todos.Clear();
        foreach (var u in query.OrderBy(u => u.NombreCompleto))
            Todos.Add(u);
    }

    [RelayCommand] private void SetFiltroRol(string rol)    { FiltroRol    = rol; }
    [RelayCommand] private void SetFiltroEstado(string est) { FiltroEstado = est; }

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

    [RelayCommand]
    private async Task EliminarAsync(UsuarioDto usuario)
    {
        var ok = await Shell.Current.DisplayAlert(
            "Eliminar usuario",
            $"¿Eliminar a {usuario.NombreCompleto}? Esta acción es irreversible.",
            "Eliminar", "Cancelar");
        if (!ok) return;

        var (exito, error) = await _api.EliminarUsuarioAsync(usuario.Id);
        if (exito)
            await CargarAsync();
        else
            await Shell.Current.DisplayAlert("Error", error ?? "No se pudo eliminar el usuario.", "OK");
    }
}
