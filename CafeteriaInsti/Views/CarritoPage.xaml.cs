// Views/CarritoPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class CarritoPage : ContentPage
    {
        public CarritoPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Obtener el ViewModel desde DI si no existe
            if (BindingContext == null)
            {
                BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<CarritoViewModel>();
            }
            
            // Actualizar carrito
            if (BindingContext is CarritoViewModel viewModel)
            {
                System.Diagnostics.Debug.WriteLine("[INFO] CarritoPage - OnAppearing");
                viewModel.ActualizarCarrito();
            }
        }
    }
}
