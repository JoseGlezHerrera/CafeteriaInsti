// Views/HistorialPedidosPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class HistorialPedidosPage : ContentPage
    {
        public HistorialPedidosPage(HistorialPedidosViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
