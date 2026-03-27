using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class AdminUsuariosPage : ContentPage
{
    private AdminUsuariosViewModel Vm => (AdminUsuariosViewModel)BindingContext;

    // La tarjeta actualmente en foco (para revertir la escala al cerrar)
    private VisualElement? _tarjetaActiva;

    // Evita que dos taps simultáneos abran el panel dos veces
    private bool _panelAnimando;

    public AdminUsuariosPage(AdminUsuariosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    // ── Apertura del panel ────────────────────────────────────────────────────

    private async void OnTarjetaTapped(object sender, TappedEventArgs e)
    {
        if (_panelAnimando) return;
        if (sender is not VisualElement tarjeta) return;
        if (tarjeta.BindingContext is not UsuarioDto usuario) return;

        await AbrirPanelAsync(tarjeta, usuario);
    }

    private async Task AbrirPanelAsync(VisualElement tarjeta, UsuarioDto usuario)
    {
        _panelAnimando = true;

        // Si ya había una tarjeta activa, la restauramos sin animación de cierre
        if (_tarjetaActiva is not null)
            await _tarjetaActiva.ScaleTo(1.0, 80);

        _tarjetaActiva = tarjeta;
        Vm.SeleccionarUsuario(usuario);

        // Preparar panel fuera de pantalla antes de hacerlo visible
        PanelContextual.TranslationY = 700;
        PanelContextual.Opacity      = 0;
        Overlay.Opacity              = 0;
        PanelContextual.IsVisible    = true;
        Overlay.IsVisible            = true;

        await Task.WhenAll(
            tarjeta.ScaleTo(1.04, 200, Easing.CubicOut),
            Overlay.FadeTo(0.55, 220),
            PanelContextual.TranslateTo(0, 0, 300, Easing.CubicOut),
            PanelContextual.FadeTo(1, 220)
        );

        _panelAnimando = false;
    }

    // ── Cierre del panel ──────────────────────────────────────────────────────

    private async Task CerrarPanelAsync(bool recargar = false)
    {
        if (_panelAnimando) return;
        _panelAnimando = true;

        var tarjeta = _tarjetaActiva;
        _tarjetaActiva = null;
        Vm.LimpiarSeleccion();

        await Task.WhenAll(
            tarjeta?.ScaleTo(1.0, 180, Easing.CubicIn) ?? Task.CompletedTask,
            Overlay.FadeTo(0, 200),
            PanelContextual.TranslateTo(0, 700, 260, Easing.CubicIn),
            PanelContextual.FadeTo(0, 200)
        );

        Overlay.IsVisible         = false;
        PanelContextual.IsVisible = false;
        _panelAnimando            = false;

        if (recargar) await Vm.CargarAsync();
    }

    // ── Handlers de cierre ────────────────────────────────────────────────────

    private async void OnOverlayTapped(object sender, TappedEventArgs e)
        => await CerrarPanelAsync();

    private async void OnCerrarPanelClicked(object sender, EventArgs e)
        => await CerrarPanelAsync();

    // ── Handlers de acciones del panel ───────────────────────────────────────
    // Patrón: cerrar panel → ejecutar comando (los confirmations aparecen
    // sobre el contenido principal, que es el comportamiento correcto).

    private async void OnPanelAprobarClicked(object sender, EventArgs e)
    {
        var u = Vm.UsuarioSeleccionado;
        await CerrarPanelAsync();
        if (u is not null) Vm.AprobarCommand.Execute(u);
    }

    private async void OnPanelRechazarClicked(object sender, EventArgs e)
    {
        var u = Vm.UsuarioSeleccionado;
        await CerrarPanelAsync();
        if (u is not null) Vm.RechazarCommand.Execute(u);
    }

    private async void OnPanelDesayunoClicked(object sender, EventArgs e)
    {
        var u = Vm.UsuarioSeleccionado;
        await CerrarPanelAsync();
        if (u is not null) Vm.ToggleDesayunoCommand.Execute(u);
    }

    private async void OnPanelSuspenderClicked(object sender, EventArgs e)
    {
        var u = Vm.UsuarioSeleccionado;
        await CerrarPanelAsync();
        if (u is not null) Vm.SuspenderCommand.Execute(u);
    }

    private async void OnPanelReactivarClicked(object sender, EventArgs e)
    {
        var u = Vm.UsuarioSeleccionado;
        await CerrarPanelAsync();
        if (u is not null) Vm.ReactivarCommand.Execute(u);
    }

    private async void OnPanelEliminarClicked(object sender, EventArgs e)
    {
        var u = Vm.UsuarioSeleccionado;
        await CerrarPanelAsync();
        if (u is not null) Vm.EliminarCommand.Execute(u);
    }
}
