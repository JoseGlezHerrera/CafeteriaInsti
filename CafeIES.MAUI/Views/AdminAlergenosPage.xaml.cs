using CafeIES.MAUI.ViewModels;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Views;

public partial class AdminAlergenosPage : ContentPage
{
    private readonly AdminAlergenosViewModel _vm;

    public AdminAlergenosPage(AdminAlergenosViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private async void OnEliminarClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is AlergenoDto alergeno)
            await _vm.EliminarAsync(alergeno);
    }
}
