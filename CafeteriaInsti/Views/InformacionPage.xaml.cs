// Views/InformacionPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class InformacionPage : ContentPage
    {
        public InformacionPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext == null)
            {
                BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<InformacionViewModel>();
            }
        }
    }
}
