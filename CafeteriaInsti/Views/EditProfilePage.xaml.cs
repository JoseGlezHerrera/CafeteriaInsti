using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class EditProfilePage : ContentPage
    {
        private readonly EditProfileViewModel _viewModel;

        public EditProfilePage(EditProfileViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }
    }
}
