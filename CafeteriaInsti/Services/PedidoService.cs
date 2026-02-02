// Services/PedidoService.cs
using CafeteriaInsti.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace CafeteriaInsti.Services
{
    public class PedidoService
    {
        private const string PEDIDOS_KEY = "pedidos_historial";
        private readonly ObservableCollection<Pedido> _pedidos = new();

        public ObservableCollection<Pedido> Pedidos => _pedidos;

        public PedidoService()
        {
            CargarPedidos();
        }

        public void GuardarPedido(Pedido pedido)
        {
            _pedidos.Insert(0, pedido); // Insertar al inicio (más reciente primero)
            PersistirPedidos();
            System.Diagnostics.Debug.WriteLine($"[INFO] Pedido guardado: {pedido.NumeroPedido}");
        }

        public Pedido? ObtenerPedido(string numeroPedido)
        {
            return _pedidos.FirstOrDefault(p => p.NumeroPedido == numeroPedido);
        }

        public List<Pedido> ObtenerPedidosRecientes(int cantidad = 10)
        {
            return _pedidos.Take(cantidad).ToList();
        }

        private void CargarPedidos()
        {
            try
            {
                var json = Preferences.Get(PEDIDOS_KEY, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    var pedidos = JsonSerializer.Deserialize<List<Pedido>>(json);
                    if (pedidos != null)
                    {
                        _pedidos.Clear();
                        foreach (var pedido in pedidos)
                        {
                            _pedidos.Add(pedido);
                        }
                        System.Diagnostics.Debug.WriteLine($"[INFO] Cargados {_pedidos.Count} pedidos del historial");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Cargar pedidos: {ex.Message}");
            }
        }

        private void PersistirPedidos()
        {
            try
            {
                var json = JsonSerializer.Serialize(_pedidos.ToList());
                Preferences.Set(PEDIDOS_KEY, json);
                System.Diagnostics.Debug.WriteLine($"[INFO] Pedidos persistidos: {_pedidos.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Persistir pedidos: {ex.Message}");
            }
        }

        public void LimpiarHistorial()
        {
            _pedidos.Clear();
            Preferences.Remove(PEDIDOS_KEY);
        }
    }
}

