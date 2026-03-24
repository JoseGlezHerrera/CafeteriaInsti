using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class AdminUsuariosPage : ContentPage
{
    private AdminUsuariosViewModel Vm => (AdminUsuariosViewModel)BindingContext;

    public AdminUsuariosPage(AdminUsuariosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = Vm.CargarAsync();
    }

    private void OnAprobarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is UsuarioDto u)
            Vm.AprobarCommand.Execute(u);
    }

    private void OnRechazarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is UsuarioDto u)
            Vm.RechazarCommand.Execute(u);
    }

    private void OnSuspenderClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is UsuarioDto u)
            Vm.SuspenderCommand.Execute(u);
    }

    private void OnReactivarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is UsuarioDto u)
            Vm.ReactivarCommand.Execute(u);
    }

    private void OnEliminarClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is UsuarioDto u)
            Vm.EliminarCommand.Execute(u);
    }
}
