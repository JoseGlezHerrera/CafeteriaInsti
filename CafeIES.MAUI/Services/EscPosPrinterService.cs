using CafeIES.Shared.Models;
using System.Net.Sockets;
using System.Text;

namespace CafeIES.MAUI.Services;

/// <summary>
/// Imprime tickets directamente en impresoras ESC/POS por red (TCP).
/// No usa el PrintManager de Android — envía bytes ESC/POS directamente al puerto 9100.
/// Compatible con AVPos TC300 y cualquier impresora térmica con conexión Ethernet.
/// </summary>
public class EscPosPrinterService
{
    private const int TimeoutMs = 5000;

    // IP y puerto fijos de la AVPos TC300
    public const string IpImpresora = "192.168.30.10";
    public const int PuertoImpresora = 9100;

    public async Task ImprimirAsync(PedidoDto p, string? ip = null, int puerto = PuertoImpresora)
    {
        ip ??= IpImpresora;
        var bytes = GenerarEscPos(p);

        using var client = new TcpClient();

        // Timeout manual: ConnectAsync no acepta CancellationToken en .NET 6 (MAUI target)
        var connectTask = client.ConnectAsync(ip, puerto);
        if (await Task.WhenAny(connectTask, Task.Delay(TimeoutMs)) != connectTask)
            throw new TimeoutException($"No se pudo conectar a {ip}:{puerto} en {TimeoutMs / 1000} s. " +
                                       "Comprueba que la impresora está encendida y en la misma red.");

        await connectTask; // propaga excepción si la conexión falló

        using var stream = client.GetStream();
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    // ── Generación ESC/POS ────────────────────────────────────────────────────

    private static byte[] GenerarEscPos(PedidoDto p)
    {
        var buf = new List<byte>();

        // ISO-8859-1: soportado por la gran mayoría de impresoras térmicas.
        // Los caracteres que no tiene (tildes, ñ) se normalizan en Norm().
        var enc = Encoding.GetEncoding("ISO-8859-1");

        // Comandos ESC/POS
        byte[] Init = { 0x1B, 0x40 };             // Inicializar impresora
        byte[] AlignCenter = { 0x1B, 0x61, 0x01 };       // Alinear centro
        byte[] AlignLeft = { 0x1B, 0x61, 0x00 };       // Alinear izquierda
        byte[] BoldOn = { 0x1B, 0x45, 0x01 };       // Negrita on
        byte[] BoldOff = { 0x1B, 0x45, 0x00 };       // Negrita off
        byte[] FontBig = { 0x1D, 0x21, 0x11 };       // Doble ancho + doble alto
        byte[] FontNormal = { 0x1D, 0x21, 0x00 };       // Tamaño normal
        byte[] CutPaper = { 0x1D, 0x56, 0x42, 0x00 }; // Corte parcial

        buf.AddRange(Init);

        // ── Cabecera ──────────────────────────────────────────────────────────
        buf.AddRange(AlignCenter);
        buf.AddRange(FontBig);
        buf.AddRange(BoldOn);
        buf.AddRange(enc.GetBytes("CAFETERIA\n"));
        buf.AddRange(BoldOff);
        buf.AddRange(FontNormal);

        if (!string.IsNullOrWhiteSpace(p.InstitutoNombre))
            buf.AddRange(enc.GetBytes(Norm(p.InstitutoNombre) + "\n"));

        buf.AddRange(enc.GetBytes(Linea('-')));

        // ── Info del pedido ───────────────────────────────────────────────────
        buf.AddRange(AlignLeft);

        var horaLocal = DateTime.SpecifyKind(p.FechaCreacion, DateTimeKind.Utc).ToLocalTime();
        buf.AddRange(enc.GetBytes($"Pedido : #{p.NumeroPedido:D3}\n"));
        buf.AddRange(enc.GetBytes($"Hora   : {horaLocal:dd/MM/yy HH:mm}\n"));
        buf.AddRange(enc.GetBytes($"Cliente: {Norm(p.UsuarioNombre)}\n"));

        buf.AddRange(enc.GetBytes(Linea('-')));

        // ── Líneas del pedido ─────────────────────────────────────────────────
        foreach (var l in p.Lineas)
        {
            buf.AddRange(BoldOn);
            buf.AddRange(enc.GetBytes(FilaProducto(Norm(l.ProductoNombre), l.Cantidad, l.Subtotal)));
            buf.AddRange(BoldOff);

            if (l.Ingredientes is { Count: > 0 })
            {
                foreach (var ing in l.Ingredientes)
                {
                    var accion = ing.Accion == AccionIngrediente.Quitar ? "sin" : "+";
                    var cant = ing.Cantidad > 1 ? $" x{ing.Cantidad}" : string.Empty;
                    var extra = ing.Accion == AccionIngrediente.Añadir && ing.PrecioAplicado > 0
                        ? $" (+{ing.PrecioAplicado * ing.Cantidad:F2})"
                        : string.Empty;
                    buf.AddRange(enc.GetBytes($"  {accion} {Norm(ing.Nombre)}{cant}{extra}\n"));
                }
            }

            if (l.Alergenos is { Count: > 0 })
            {
                var lista = string.Join(", ", l.Alergenos.Select(a => Norm(a.Nombre)));
                buf.AddRange(enc.GetBytes($"  Alergenos: {lista}\n"));
            }

            if (!string.IsNullOrWhiteSpace(l.Notas))
                buf.AddRange(enc.GetBytes($"  Nota: {Norm(l.Notas)}\n"));
        }

        // ── Nota global del pedido ────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(p.Notas))
        {
            buf.AddRange(enc.GetBytes(Linea('-')));
            buf.AddRange(BoldOn);
            buf.AddRange(enc.GetBytes($"NOTA: {Norm(p.Notas)}\n"));
            buf.AddRange(BoldOff);
        }

        // ── Total ─────────────────────────────────────────────────────────────
        buf.AddRange(enc.GetBytes(Linea('=')));
        buf.AddRange(AlignCenter);
        buf.AddRange(FontBig);
        buf.AddRange(BoldOn);
        buf.AddRange(enc.GetBytes($"TOTAL: {p.Total:F2} EUR\n"));
        buf.AddRange(BoldOff);
        buf.AddRange(FontNormal);
        buf.AddRange(AlignLeft);

        var metodoPago = p.MetodoPago switch
        {
            MetodoPago.Tarjeta => "Tarjeta",
            MetodoPago.GooglePay => "Google Pay",
            MetodoPago.ApplePay => "Apple Pay",
            MetodoPago.Gratuito => "Gratuito",
            _ => p.MetodoPago.ToString()
        };
        buf.AddRange(enc.GetBytes($"Pago: {metodoPago}\n"));

        // ── Pie ───────────────────────────────────────────────────────────────
        buf.AddRange(enc.GetBytes(Linea('-')));
        buf.AddRange(AlignCenter);
        buf.AddRange(enc.GetBytes("Buen provecho!\n\n\n"));

        // ── Corte de papel ────────────────────────────────────────────────────
        buf.AddRange(CutPaper);

        return buf.ToArray();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Normaliza caracteres que ISO-8859-1 no representa bien en impresoras térmicas.
    /// Más fiable que cambiar la página de códigos con ESC t.
    /// </summary>
    private static string Norm(string? s)
    {
        if (s is null) return string.Empty;
        return s
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ü", "u")
            .Replace("Á", "A").Replace("É", "E").Replace("Í", "I")
            .Replace("Ó", "O").Replace("Ú", "U").Replace("Ü", "U")
            .Replace("ñ", "n").Replace("Ñ", "N")
            .Replace("¡", "!").Replace("¿", "?")
            .Replace("€", "EUR");
    }

    /// <summary>Línea separadora de 32 caracteres.</summary>
    private static string Linea(char c) => new string(c, 32) + "\n";

    /// <summary>
    /// Fila de producto con nombre a la izquierda y "xN  0.00" a la derecha,
    /// ajustada a 32 columnas (ancho estándar de rollo 80 mm).
    /// </summary>
    private static string FilaProducto(string nombre, int cantidad, decimal subtotal)
    {
        var derecha = $"x{cantidad}  {subtotal:F2}";
        var maxNombre = 32 - derecha.Length - 1;
        if (nombre.Length > maxNombre)
            nombre = nombre[..maxNombre];
        return nombre.PadRight(32 - derecha.Length) + derecha + "\n";
    }
}