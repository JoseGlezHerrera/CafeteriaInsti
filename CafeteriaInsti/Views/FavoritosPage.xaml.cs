// Views/FavoritosPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class FavoritosPage : ContentPage
    {
        public FavoritosPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            if (BindingContext == null)
            {
                BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<FavoritosViewModel>();
            }
            
            if (BindingContext is FavoritosViewModel viewModel)
            {
                viewModel.CargarFavoritos();
            }
        }
    }
}

