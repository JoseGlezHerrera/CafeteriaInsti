// Views/DetalleProductoPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class DetalleProductoPage : ContentPage
    {
        public DetalleProductoPage()
        {
            InitializeComponent();
            BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<DetalleProductoViewModel>();
        }
    }
}
