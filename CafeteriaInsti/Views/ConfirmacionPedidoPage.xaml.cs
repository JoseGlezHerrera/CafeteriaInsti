// Views/ConfirmacionPedidoPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class ConfirmacionPedidoPage : ContentPage
    {
        private readonly ConfirmacionPedidoViewModel _viewModel;

        public ConfirmacionPedidoPage(ConfirmacionPedidoViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
            System.Diagnostics.Debug.WriteLine("[INFO] ConfirmacionPedidoPage - Constructor llamado");
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            System.Diagnostics.Debug.WriteLine("[INFO] ConfirmacionPedidoPage - OnNavigatedTo");
            System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedidoPage - NumeroPedido: {_viewModel.NumeroPedido}");
            System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedidoPage - Total: {_viewModel.Total:C}");
            System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedidoPage - CantidadItems: {_viewModel.CantidadItems}");
        }
    }
}
