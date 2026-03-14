using CafeIES.Shared.Models;
using ClosedXML.Excel;

namespace CafeIES.API.Services;

public static class ReporteExcelService
{
    public static byte[] Generar(List<Pedido> pedidos, DateTime? desde, DateTime? hasta)
    {
        var completados = pedidos.Where(p => p.Estado != EstadoPedido.Cancelado).ToList();

        using var wb = new XLWorkbook();

        // ── Hoja 1: Resumen ───────────────────────────────────────────────────
        var wsRes = wb.Worksheets.Add("Resumen");
        wsRes.Column(1).Width = 30;
        wsRes.Column(2).Width = 20;

        // Título
        wsRes.Cell("A1").Value = "Reporte CaféIES";
        wsRes.Cell("A1").Style.Font.Bold        = true;
        wsRes.Cell("A1").Style.Font.FontSize    = 16;

        wsRes.Cell("A2").Value = $"Período: {desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}";
        wsRes.Cell("A2").Style.Font.Italic      = true;
        wsRes.Cell("A2").Style.Font.FontColor   = XLColor.Gray;

        wsRes.Cell("A4").Value = "KPI";
        wsRes.Cell("B4").Value = "Valor";
        EstiloEncabezado(wsRes.Range("A4:B4"));

        var kpis = new[]
        {
            ("Pedidos totales",     (object)pedidos.Count),
            ("Pedidos completados", (object)completados.Count),
            ("Pedidos cancelados",  (object)pedidos.Count(p => p.Estado == EstadoPedido.Cancelado)),
            ("Ingresos totales (€)",(object)completados.Sum(p => p.Total)),
            ("Ticket medio (€)",    (object)(completados.Count > 0 ? completados.Average(p => p.Total) : 0m)),
            ("Usuarios únicos",     (object)pedidos.Select(p => p.UsuarioId).Distinct().Count()),
        };

        for (int i = 0; i < kpis.Length; i++)
        {
            wsRes.Cell(5 + i, 1).Value = kpis[i].Item1;
            var cell = wsRes.Cell(5 + i, 2);
            cell.Value = kpis[i].Item2 is decimal d ? d : (int)kpis[i].Item2;
            if (kpis[i].Item2 is decimal)
                cell.Style.NumberFormat.Format = "#,##0.00";
            if (i % 2 == 0)
                wsRes.Row(5 + i).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9F9F9");
        }

        // ── Hoja 2: Pedidos ───────────────────────────────────────────────────
        var wsPed = wb.Worksheets.Add("Pedidos");
        var headers = new[] { "Nº Pedido", "Fecha", "Usuario", "Email", "Estado", "Método Pago", "Total (€)", "Notas" };
        for (int i = 0; i < headers.Length; i++)
            wsPed.Cell(1, i + 1).Value = headers[i];
        EstiloEncabezado(wsPed.Range(1, 1, 1, headers.Length));

        for (int i = 0; i < pedidos.Count; i++)
        {
            var p   = pedidos[i];
            var row = i + 2;
            wsPed.Cell(row, 1).Value = p.NumeroPedido;
            wsPed.Cell(row, 2).Value = p.FechaCreacion;
            wsPed.Cell(row, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            wsPed.Cell(row, 3).Value = p.Usuario?.NombreCompleto ?? "";
            wsPed.Cell(row, 4).Value = p.Usuario?.Email ?? "";
            wsPed.Cell(row, 5).Value = p.Estado.ToString();
            wsPed.Cell(row, 6).Value = p.MetodoPago.ToString();
            wsPed.Cell(row, 7).Value = p.Total;
            wsPed.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            wsPed.Cell(row, 8).Value = p.Notas ?? "";

            if (p.Estado == EstadoPedido.Cancelado)
                wsPed.Row(row).Style.Font.FontColor = XLColor.Red;
            else if (i % 2 == 0)
                wsPed.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9F9F9");
        }
        wsPed.Columns().AdjustToContents();

        // ── Hoja 3: Ranking de Productos ─────────────────────────────────────
        var wsRank = wb.Worksheets.Add("Ranking Productos");
        wsRank.Cell(1, 1).Value = "Producto";
        wsRank.Cell(1, 2).Value = "Unidades";
        wsRank.Cell(1, 3).Value = "Ingresos (€)";
        EstiloEncabezado(wsRank.Range("A1:C1"));

        var ranking = completados
            .SelectMany(p => p.Lineas)
            .GroupBy(l => l.Producto?.Nombre ?? "Desconocido")
            .Select(g => (Nombre: g.Key, Unidades: g.Sum(l => l.Cantidad), Ingresos: g.Sum(l => l.Subtotal)))
            .OrderByDescending(x => x.Unidades)
            .ToList();

        for (int i = 0; i < ranking.Count; i++)
        {
            var r   = ranking[i];
            var row = i + 2;
            wsRank.Cell(row, 1).Value = r.Nombre;
            wsRank.Cell(row, 2).Value = r.Unidades;
            wsRank.Cell(row, 3).Value = r.Ingresos;
            wsRank.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            if (i % 2 == 0)
                wsRank.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9F9F9");
        }
        wsRank.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void EstiloEncabezado(IXLRange range)
    {
        range.Style.Font.Bold             = true;
        range.Style.Fill.BackgroundColor  = XLColor.FromHtml("#2563EB");
        range.Style.Font.FontColor        = XLColor.White;
        range.Style.Alignment.Horizontal  = XLAlignmentHorizontalValues.Center;
    }
}
