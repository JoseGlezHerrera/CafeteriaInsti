using CafeIES.Shared.Models;
using System.Text;

namespace CafeIES.MAUI.Services;

/// <summary>
/// Construye el HTML del ticket a partir de un PedidoDto.
/// El HTML está optimizado para impresión térmica (80 mm) y funciona
/// con el diálogo de impresión nativo de Android.
/// </summary>
public static class TicketHtmlBuilder
{
    public static string Build(PedidoDto p)
    {
        var spainTz  = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Madrid");
        var horaLocal = TimeZoneInfo.ConvertTimeFromUtc(p.FechaCreacion, spainTz);

        var metodoPago = p.MetodoPago switch
        {
            MetodoPago.Tarjeta   => "Tarjeta",
            MetodoPago.GooglePay => "Google Pay",
            MetodoPago.ApplePay  => "Apple Pay",
            MetodoPago.Gratuito  => "Gratuito",
            _                    => p.MetodoPago.ToString()
        };

        var sb = new StringBuilder();
        sb.AppendLine("""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <style>
              * { box-sizing: border-box; margin: 0; padding: 0; }
              body {
                font-family: 'Courier New', Courier, monospace;
                font-size: 13px; color: #000; background: #fff;
                max-width: 300px; margin: 0 auto; padding: 10px;
              }
              @media print { body { max-width: 100%; } }
              .center  { text-align: center; }
              .bold    { font-weight: bold; }
              .title   { font-size: 20px; font-weight: bold; text-align: center; letter-spacing: 2px; }
              .inst    { text-align: center; font-size: 11px; color: #555; margin-bottom: 10px; }
              .dash    { border-top: 1px dashed #000; margin: 7px 0; }
              .row     { display: flex; justify-content: space-between; margin: 2px 0; }
              .prod    { font-weight: bold; margin-top: 6px; }
              .mod     { padding-left: 14px; font-size: 11px; color: #444; }
              .subtotal{ text-align: right; font-size: 11px; color: #555; }
              .total   { font-size: 15px; font-weight: bold; }
              .footer  { margin-top: 14px; text-align: center; font-size: 11px; color: #666; }
            </style>
            </head>
            <body>
            """);

        sb.AppendLine("<div class=\"title\">CAFETER&Iacute;A</div>");
        if (!string.IsNullOrWhiteSpace(p.InstitutoNombre))
            sb.AppendLine($"<div class=\"inst\">{Esc(p.InstitutoNombre)}</div>");

        sb.AppendLine("<div class=\"dash\"></div>");
        sb.AppendLine($"<div class=\"row\"><span>Pedido</span><span class=\"bold\">#{p.NumeroPedido:D3}</span></div>");
        sb.AppendLine($"<div class=\"row\"><span>Hora</span><span>{horaLocal:dd/MM/yy HH:mm}</span></div>");
        sb.AppendLine($"<div class=\"row\"><span>Cliente</span><span>{Esc(p.UsuarioNombre)}</span></div>");
        sb.AppendLine("<div class=\"dash\"></div>");

        // Líneas del pedido
        foreach (var l in p.Lineas)
        {
            sb.AppendLine($"<div class=\"row prod\"><span>{Esc(l.ProductoNombre)}</span><span>x{l.Cantidad}</span></div>");

            if (l.Ingredientes is { Count: > 0 })
            {
                foreach (var ing in l.Ingredientes)
                {
                    var accion = ing.Accion == AccionIngrediente.Quitar ? "sin" : "+";
                    var extra  = ing.Cantidad > 1 ? $" x{ing.Cantidad}" : string.Empty;
                    sb.AppendLine($"<div class=\"mod\">{accion} {Esc(ing.Nombre)}{extra}</div>");
                }
            }

            if (l.Subtotal > 0)
                sb.AppendLine($"<div class=\"subtotal\">{l.Subtotal:F2} &euro;</div>");
        }

        if (!string.IsNullOrWhiteSpace(p.Notas))
        {
            sb.AppendLine("<div class=\"dash\"></div>");
            sb.AppendLine($"<div class=\"mod\">Nota: {Esc(p.Notas)}</div>");
        }

        sb.AppendLine("<div class=\"dash\"></div>");
        sb.AppendLine($"<div class=\"row total\"><span>TOTAL</span><span>{p.Total:F2} &euro;</span></div>");
        sb.AppendLine($"<div class=\"row\"><span>Pago</span><span>{metodoPago}</span></div>");
        sb.AppendLine("<div class=\"footer\">&#x1F37D;&#xFE0F; &iexcl;Buen provecho!</div>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }

    private static string Esc(string? s) =>
        s is null ? string.Empty
        : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
