// Views/DetalleProductoPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class DetalleProductoPage : ContentPage
    {
        public DetalleProductoPage(DetalleProductoViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}