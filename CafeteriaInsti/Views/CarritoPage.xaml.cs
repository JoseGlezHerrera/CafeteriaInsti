// Views/CarritoPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class CarritoPage : ContentPage
    {
        private readonly CarritoViewModel _viewModel;

        public CarritoPage(CarritoViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        // ? NUEVO: Actualizar carrito cada vez que se muestra la página
        protected override void OnAppearing()
        {
            base.OnAppearing();
            System.Diagnostics.Debug.WriteLine("[INFO] CarritoPage - OnAppearing");
            _viewModel.ActualizarCarrito();
        }
    }
}