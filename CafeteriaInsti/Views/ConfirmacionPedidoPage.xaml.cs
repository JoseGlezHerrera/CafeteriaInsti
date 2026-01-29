// Views/ConfirmacionPedidoPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class ConfirmacionPedidoPage : ContentPage
    {
        public ConfirmacionPedidoPage(ConfirmacionPedidoViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
