// Views/ConfirmacionPedidoPage.xaml.cs
using CafeteriaInsti.ViewModels;

namespace CafeteriaInsti.Views
{
    public partial class ConfirmacionPedidoPage : ContentPage
    {
        public ConfirmacionPedidoPage()
        {
            InitializeComponent();
            BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<ConfirmacionPedidoViewModel>();
            System.Diagnostics.Debug.WriteLine("[INFO] ConfirmacionPedidoPage - Constructor llamado");
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            System.Diagnostics.Debug.WriteLine("[INFO] ConfirmacionPedidoPage - OnNavigatedTo");
            
            if (BindingContext is ConfirmacionPedidoViewModel viewModel)
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedidoPage - NumeroPedido: {viewModel.NumeroPedido}");
                System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedidoPage - Total: {viewModel.Total:C}");
                System.Diagnostics.Debug.WriteLine($"[INFO] ConfirmacionPedidoPage - CantidadItems: {viewModel.CantidadItems}");
            }
        }
    }
}

