// Views/ListaProductosPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class ListaProductosPage : ContentPage
    {
        public ListaProductosPage(ListaProductosViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
