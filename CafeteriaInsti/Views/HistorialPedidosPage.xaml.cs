// Views/HistorialPedidosPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class HistorialPedidosPage : ContentPage
    {
        public HistorialPedidosPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext == null)
            {
                BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<HistorialPedidosViewModel>();
            }
        }
    }
}
