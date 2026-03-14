using CafeIES.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CafeIES.API.Services;

public static class ReportePdfService
{
    public static byte[] Generar(List<Pedido> pedidos, DateTime? desde, DateTime? hasta)
    {
        var completados = pedidos.Where(p => p.Estado != EstadoPedido.Cancelado).ToList();
        var ingresos    = completados.Sum(p => p.Total);
        var ticketMedio = completados.Count > 0 ? completados.Average(p => p.Total) : 0m;

        var ranking = completados
            .SelectMany(p => p.Lineas)
            .GroupBy(l => l.Producto?.Nombre ?? "Desconocido")
            .Select(g => (Nombre: g.Key, Unidades: g.Sum(l => l.Cantidad), Ingresos: g.Sum(l => l.Subtotal)))
            .OrderByDescending(x => x.Unidades)
            .Take(10)
            .ToList();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                // ── Encabezado ────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("☕ CaféIES — Reporte de actividad")
                            .FontSize(18).Bold().FontColor(Color.FromHex("#1E3A5F"));
                        row.ConstantItem(100).AlignRight()
                            .Text($"Generado: {DateTime.Now:dd/MM/yyyy}")
                            .FontSize(8).FontColor(Color.FromHex("#6B7280"));
                    });
                    col.Item().PaddingTop(4).BorderBottom(1).BorderColor(Color.FromHex("#2563EB")).Text("");
                    col.Item().PaddingTop(4)
                        .Text($"Período: {desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}")
                        .FontSize(10).Italic().FontColor(Color.FromHex("#6B7280"));
                });

                // ── Contenido ─────────────────────────────────────────────────
                page.Content().PaddingTop(16).Column(col =>
                {
                    // KPIs
                    col.Item().Text("Resumen").FontSize(13).Bold().FontColor(Color.FromHex("#1E3A5F"));
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                        });

                        // Header
                        TableHeader(table, "Indicador", "Valor");

                        // Rows
                        TableRow(table, "Pedidos totales",     pedidos.Count.ToString(), true);
                        TableRow(table, "Pedidos completados", completados.Count.ToString(), false);
                        TableRow(table, "Pedidos cancelados",  pedidos.Count(p => p.Estado == EstadoPedido.Cancelado).ToString(), true);
                        TableRow(table, "Ingresos totales",    $"{ingresos:F2} €", false);
                        TableRow(table, "Ticket medio",        $"{ticketMedio:F2} €", true);
                        TableRow(table, "Usuarios únicos",     pedidos.Select(p => p.UsuarioId).Distinct().Count().ToString(), false);
                    });

                    // Métodos de pago
                    var metodosGrupos = completados
                        .GroupBy(p => p.MetodoPago)
                        .Select(g => (Metodo: g.Key.ToString(), Count: g.Count(), Total: g.Sum(p => p.Total)))
                        .OrderByDescending(x => x.Count)
                        .ToList();

                    if (metodosGrupos.Any())
                    {
                        col.Item().PaddingTop(20)
                            .Text("Métodos de pago").FontSize(13).Bold().FontColor(Color.FromHex("#1E3A5F"));
                        col.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(1);
                                c.RelativeColumn(2);
                            });

                            TableHeader(table, "Método", "Pedidos", "Total (€)");
                            for (int i = 0; i < metodosGrupos.Count; i++)
                            {
                                var m = metodosGrupos[i];
                                TableRow3(table, m.Metodo, m.Count.ToString(), $"{m.Total:F2} €", i % 2 == 0);
                            }
                        });
                    }

                    // Ranking
                    if (ranking.Any())
                    {
                        col.Item().PaddingTop(20)
                            .Text("Top 10 productos más vendidos").FontSize(13).Bold().FontColor(Color.FromHex("#1E3A5F"));
                        col.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(25);
                                c.RelativeColumn(4);
                                c.RelativeColumn(1);
                                c.RelativeColumn(2);
                            });

                            TableHeader4(table, "#", "Producto", "Unidades", "Ingresos (€)");
                            for (int i = 0; i < ranking.Count; i++)
                            {
                                var r = ranking[i];
                                TableRow4(table, (i + 1).ToString(), r.Nombre, r.Unidades.ToString(), $"{r.Ingresos:F2} €", i % 2 == 0);
                            }
                        });
                    }
                });

                // ── Pie ───────────────────────────────────────────────────────
                page.Footer().AlignCenter()
                    .Text(t =>
                    {
                        t.Span("CaféIES — Reporte generado automáticamente · Página ").FontSize(8).FontColor(Color.FromHex("#9CA3AF"));
                        t.CurrentPageNumber().FontSize(8).FontColor(Color.FromHex("#9CA3AF"));
                        t.Span(" de ").FontSize(8).FontColor(Color.FromHex("#9CA3AF"));
                        t.TotalPages().FontSize(8).FontColor(Color.FromHex("#9CA3AF"));
                    });
            });
        });

        return doc.GeneratePdf();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void TableHeader(TableDescriptor table, string col1, string col2)
    {
        table.Header(h =>
        {
            h.Cell().Background(Color.FromHex("#2563EB")).Padding(5)
                .Text(col1).FontColor(Colors.White).Bold().FontSize(10);
            h.Cell().Background(Color.FromHex("#2563EB")).Padding(5)
                .Text(col2).FontColor(Colors.White).Bold().FontSize(10);
        });
    }

    private static void TableRow(TableDescriptor table, string label, string value, bool alt)
    {
        var bg = alt ? Color.FromHex("#F9F9F9") : Colors.White;
        table.Cell().Background(bg).Padding(4).Text(label).FontSize(9);
        table.Cell().Background(bg).Padding(4).AlignRight().Text(value).FontSize(9);
    }

    private static void TableHeader(TableDescriptor table, string c1, string c2, string c3)
    {
        table.Header(h =>
        {
            foreach (var col in new[] { c1, c2, c3 })
                h.Cell().Background(Color.FromHex("#2563EB")).Padding(5)
                    .Text(col).FontColor(Colors.White).Bold().FontSize(10);
        });
    }

    private static void TableRow3(TableDescriptor table, string c1, string c2, string c3, bool alt)
    {
        var bg = alt ? Color.FromHex("#F9F9F9") : Colors.White;
        table.Cell().Background(bg).Padding(4).Text(c1).FontSize(9);
        table.Cell().Background(bg).Padding(4).AlignCenter().Text(c2).FontSize(9);
        table.Cell().Background(bg).Padding(4).AlignRight().Text(c3).FontSize(9);
    }

    private static void TableHeader4(TableDescriptor table, string c1, string c2, string c3, string c4)
    {
        table.Header(h =>
        {
            foreach (var col in new[] { c1, c2, c3, c4 })
                h.Cell().Background(Color.FromHex("#2563EB")).Padding(5)
                    .Text(col).FontColor(Colors.White).Bold().FontSize(10);
        });
    }

    private static void TableRow4(TableDescriptor table, string c1, string c2, string c3, string c4, bool alt)
    {
        var bg = alt ? Color.FromHex("#F9F9F9") : Colors.White;
        table.Cell().Background(bg).Padding(4).AlignCenter().Text(c1).FontSize(9);
        table.Cell().Background(bg).Padding(4).Text(c2).FontSize(9);
        table.Cell().Background(bg).Padding(4).AlignCenter().Text(c3).FontSize(9);
        table.Cell().Background(bg).Padding(4).AlignRight().Text(c4).FontSize(9);
    }
}
