// Views/InformacionPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class InformacionPage : ContentPage
    {
        public InformacionPage(InformacionViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
