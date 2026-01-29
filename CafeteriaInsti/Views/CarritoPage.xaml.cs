// Views/CarritoPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class CarritoPage : ContentPage
    {
        public CarritoPage(CarritoViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}