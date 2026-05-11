using CafeIES.Shared.Models;
using System.Linq;
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
        // ToLocalTime() usa la zona horaria del sistema del dispositivo (para un móvil
        // español eso es Europe/Madrid con CEST automático). SpecifyKind garantiza que
        // la conversión parte siempre de UTC independientemente de lo que devuelva el JSON.
        var horaLocal = DateTime.SpecifyKind(p.FechaCreacion, DateTimeKind.Utc).ToLocalTime();

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
              /* @page instruye al motor de impresión del WebView a usar 80 mm de ancho.
                 El alto "auto" deja que el contenido determine la longitud del rollo.  */
              * { box-sizing: border-box; margin: 0; padding: 0; }
              body {
                font-family: 'Courier New', Courier, monospace;
                font-size: 8pt; color: #000; background: #fff;
                max-width: 300px; margin: 0 auto; padding: 8px;
              }
              @media print { body { max-width: 300px; } }
              .center  { text-align: center; }
              .bold    { font-weight: bold; }
              .title   { font-size: 12pt; font-weight: bold; text-align: center; letter-spacing: 2px; }
              .inst    { text-align: center; font-size: 7pt; color: #555; margin-bottom: 4pt; }
              .dash    { border-top: 1px dashed #000; margin: 3pt 0; }
              .row     { display: flex; justify-content: space-between; margin: 1pt 0; }
              .prod    { font-weight: bold; margin-top: 3pt; }
              .mod     { padding-left: 8pt; font-size: 7pt; font-style: italic; color: #444; }
              .alergenos { padding-left: 8pt; font-size: 7pt; color: #7a2e2e; }
              .nota    { padding-left: 8pt; font-size: 7pt; font-style: italic; color: #444; }
              .nota-global { font-size: 7pt; font-weight: bold; }
              .subtotal{ text-align: right; font-size: 7pt; color: #555; }
              .total   { font-size: 10pt; font-weight: bold; }
              .footer  { margin-top: 6pt; text-align: center; font-size: 7pt; color: #666; }
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
                    var accion    = ing.Accion == AccionIngrediente.Quitar ? "sin" : "+";
                    var cantStr   = ing.Cantidad > 1 ? $" x{ing.Cantidad}" : string.Empty;
                    var precioStr = (ing.Accion == AccionIngrediente.Añadir && ing.PrecioAplicado > 0)
                        ? $" (+{ing.PrecioAplicado * ing.Cantidad:F2}&euro;)"
                        : string.Empty;
                    sb.AppendLine($"<div class=\"mod\">{accion} {Esc(ing.Nombre)}{cantStr}{precioStr}</div>");
                }
            }

            if (l.Alergenos is { Count: > 0 })
            {
                var alergenos = string.Join(", ", l.Alergenos.Select(a => Esc(a.Nombre)));
                sb.AppendLine($"<div class=\"alergenos\">Alérgenos: {alergenos}</div>");
            }

            if (!string.IsNullOrWhiteSpace(l.Notas))
                sb.AppendLine($"<div class=\"nota\">&#x1F4DD; {Esc(l.Notas)}</div>");

            if (l.Subtotal > 0)
                sb.AppendLine($"<div class=\"subtotal\">{l.Subtotal:F2} &euro;</div>");
        }

        if (!string.IsNullOrWhiteSpace(p.Notas))
        {
            sb.AppendLine("<div class=\"dash\"></div>");
            sb.AppendLine($"<div class=\"nota nota-global\">&#x26A0;&#xFE0F; Nota del pedido: {Esc(p.Notas)}</div>");
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
