// ViewModels/HistorialPedidosViewModel.cs
using CafeteriaInsti.Models;
using CafeteriaInsti.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CafeteriaInsti.ViewModels
{
    public partial class HistorialPedidosViewModel : BaseViewModel
    {
        private readonly PedidoService _pedidoService;

        [ObservableProperty]
        private ObservableCollection<Pedido> _pedidos;

        [ObservableProperty]
        private bool _hayPedidos;

        public HistorialPedidosViewModel(PedidoService pedidoService)
        {
            Title = "Mis Pedidos";
            _pedidoService = pedidoService;
            _pedidos = _pedidoService.Pedidos;
            
            // Actualizar cuando cambia la colección
            _pedidos.CollectionChanged += (s, e) =>
            {
                HayPedidos = _pedidos.Any();
            };
            
            HayPedidos = _pedidos.Any();
        }

        [RelayCommand]
        private async Task VerDetallePedido(Pedido pedido)
        {
            if (pedido == null) return;

            var items = string.Join("\n", pedido.Items.Select(i => $"• {i.Cantidad}x {i.Producto.Nombre}"));

            await Shell.Current.DisplayAlert(
                $"Pedido #{pedido.NumeroPedido}",
                $"Fecha: {pedido.FechaPedido:dd/MM/yyyy HH:mm}\n" +
                $"Total: {pedido.Total:C}\n\n" +
                $"Productos:\n{items}",
                "OK");
        }

        [RelayCommand]
        private async Task LimpiarHistorial()
        {
            var confirmar = await Shell.Current.DisplayAlert(
                "Limpiar Historial",
                "Estas seguro de que deseas eliminar todo el historial de pedidos?",
                "Si, eliminar",
                "Cancelar");

            if (confirmar)
            {
                _pedidoService.LimpiarHistorial();
                await Shell.Current.DisplayAlert("Historial", "Historial limpiado correctamente", "OK");
            }
        }
    }
}
