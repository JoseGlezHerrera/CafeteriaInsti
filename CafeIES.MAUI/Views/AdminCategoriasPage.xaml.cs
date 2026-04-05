using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class AdminCategoriasPage : ContentPage
{
    private readonly AdminCategoriasViewModel _vm;

    public AdminCategoriasPage(AdminCategoriasViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private async void OnEliminarClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is CategoriaDto cat)
            await _vm.EliminarAsync(cat);
    }
}
