namespace CafeIES.MAUI.Views;

[QueryProperty(nameof(NumeroPedido), "numeroPedido")]
[QueryProperty(nameof(Total),        "total")]
public partial class ConfirmacionPedidoPage : ContentPage
{
    public string NumeroPedido
    {
        set => NumeroPedidoLabel.Text = $"#{value.PadLeft(3, '0')}";
    }

    public string Total
    {
        set
        {
            if (decimal.TryParse(value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var amount))
                TotalLabel.Text = $"Total: {amount:F2}€";
            else
                TotalLabel.Text = $"Total: {value}€";
        }
    }

    public ConfirmacionPedidoPage()
    {
        InitializeComponent();
    }

    private async void OnVerPedidosClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Main/Pedidos");
    }

    private async void OnSeguirPidiendoClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Main/Inicio");
    }
}
