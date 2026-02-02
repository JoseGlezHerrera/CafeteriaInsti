using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class RegisterPage : ContentPage
    {
        private readonly RegisterViewModel _viewModel;

        public RegisterPage(RegisterViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }
    }
}
