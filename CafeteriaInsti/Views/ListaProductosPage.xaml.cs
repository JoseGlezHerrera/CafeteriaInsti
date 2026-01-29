// Views/ListaProductosPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class ListaProductosPage : ContentPage
    {
        private ListaProductosViewModel _viewModel;

        public ListaProductosPage(ListaProductosViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        private void OnCategoriaClicked(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                var categoria = button.Text.Replace("? ", "").Replace("?? ", "").Replace("?? ", "").Replace("?? ", "").Trim();
                _viewModel.CategoriaSeleccionada = categoria;
            }
        }
    }
}
