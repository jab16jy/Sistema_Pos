using System.Globalization;
using ClosedXML.Excel;
using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace POS.Infrastructure.Services;

public sealed class ReportExportService : IReportExportService
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public Task<byte[]> ExportOperationalReportPdfAsync(
        OperationalReportDto report,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Reporte Operacional").FontSize(20).Bold();
                    col.Item().Text($"Periodo: {from.ToLocalTime():yyyy-MM-dd HH:mm} — {to.ToLocalTime():yyyy-MM-dd HH:mm}").FontSize(10);
                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(3);
                            cd.RelativeColumn(2);
                        });

                        void Row(string label, decimal value)
                        {
                            table.Cell().Padding(4).Text(label);
                            table.Cell().Padding(4).AlignRight().Text(value.ToString("C0", Co));
                        }

                        table.Header(header =>
                        {
                            header.Cell().Padding(4).Text("Concepto").Bold();
                            header.Cell().Padding(4).AlignRight().Text("Monto").Bold();
                        });

                        Row("Total Facturado", report.BilledSalesTotal);
                        Row("Efectivo de Ventas", report.SaleCashCollected);
                        Row("Pagos Digitales", report.SaleDigitalCollected);
                        Row("Abonos Cobrados", report.ReceivableSettlementTotal);
                        Row("Efectivo Esperado en Caja", report.ExpectedDrawerCash);
                    });
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    public Task<byte[]> ExportOperationalReportExcelAsync(
        OperationalReportDto report,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Operacional");

        ws.Cell("A1").Value = "Reporte Operacional";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A2").Value = $"Periodo: {from.ToLocalTime():yyyy-MM-dd HH:mm} — {to.ToLocalTime():yyyy-MM-dd HH:mm}";

        ws.Cell("A4").Value = "Concepto";
        ws.Cell("B4").Value = "Monto (COP)";
        ws.Row(4).Style.Font.Bold = true;

        var rows = new[]
        {
            ("Total Facturado", report.BilledSalesTotal),
            ("Efectivo de Ventas", report.SaleCashCollected),
            ("Pagos Digitales", report.SaleDigitalCollected),
            ("Abonos Cobrados", report.ReceivableSettlementTotal),
            ("Efectivo Esperado en Caja", report.ExpectedDrawerCash),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            ws.Cell(5 + i, 1).Value = rows[i].Item1;
            ws.Cell(5 + i, 2).Value = rows[i].Item2;
            ws.Cell(5 + i, 2).Style.NumberFormat.Format = "#,##0";
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> ExportProfitReportPdfAsync(
        IReadOnlyList<TopSellerResult> topSellers,
        PeriodComparisonResult comparison,
        IReadOnlyList<ProductMarginResult> margins,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
                page.Content().Column(col =>
                {
                    col.Item().Text("Reporte de Rentabilidad").FontSize(20).Bold();
                    col.Item().Text($"Periodo: {from:yyyy-MM-dd} — {to:yyyy-MM-dd}").FontSize(10);

                    col.Item().PaddingTop(12).Text("Top Vendidos").FontSize(14).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(3);
                            cd.RelativeColumn(1);
                            cd.RelativeColumn(2);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Padding(3).Text("Producto").Bold();
                            h.Cell().Padding(3).AlignRight().Text("Unidades").Bold();
                            h.Cell().Padding(3).AlignRight().Text("Ingreso").Bold();
                        });
                        foreach (var s in topSellers)
                        {
                            table.Cell().Padding(3).Text(s.ProductName);
                            table.Cell().Padding(3).AlignRight().Text(s.UnitsSold.ToString());
                            table.Cell().Padding(3).AlignRight().Text(s.TotalRevenue.ToString("C0", Co));
                        }
                    });

                    col.Item().PaddingTop(12).Text("Comparacion de Periodos").FontSize(14).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(2);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Padding(3).Text("Ingreso Actual").Bold();
                            h.Cell().Padding(3).Text("Ingreso Anterior").Bold();
                            h.Cell().Padding(3).Text("Margen Actual").Bold();
                            h.Cell().Padding(3).Text("Crecimiento %").Bold();
                        });
                        table.Cell().Padding(3).Text(comparison.CurrentRevenue.ToString("C0", Co));
                        table.Cell().Padding(3).Text(comparison.PriorRevenue.ToString("C0", Co));
                        table.Cell().Padding(3).Text(comparison.CurrentMargin.ToString("C0", Co));
                        table.Cell().Padding(3).Text($"{comparison.RevenueGrowthPercent:N1}%");
                    });

                    col.Item().PaddingTop(12).Text("Margen por Producto").FontSize(14).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(3);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Padding(3).Text("Producto").Bold();
                            h.Cell().Padding(3).AlignRight().Text("Ingreso").Bold();
                            h.Cell().Padding(3).AlignRight().Text("Costo").Bold();
                            h.Cell().Padding(3).AlignRight().Text("Margen").Bold();
                            h.Cell().Padding(3).AlignRight().Text("%").Bold();
                        });
                        foreach (var m in margins)
                        {
                            table.Cell().Padding(3).Text(m.ProductName);
                            table.Cell().Padding(3).AlignRight().Text(m.TotalRevenue.ToString("C0", Co));
                            table.Cell().Padding(3).AlignRight().Text(m.TotalCost.ToString("C0", Co));
                            table.Cell().Padding(3).AlignRight().Text(m.GrossMargin.ToString("C0", Co));
                            table.Cell().Padding(3).AlignRight().Text($"{m.MarginPercent:N1}%");
                        }
                    });
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    public Task<byte[]> ExportProfitReportExcelAsync(
        IReadOnlyList<TopSellerResult> topSellers,
        PeriodComparisonResult comparison,
        IReadOnlyList<ProductMarginResult> margins,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();

        // Sheet 1: Top Sellers
        var ws1 = workbook.Worksheets.Add("Top Vendidos");
        ws1.Cell("A1").Value = $"Top Vendidos — {from:yyyy-MM-dd} / {to:yyyy-MM-dd}";
        ws1.Cell("A1").Style.Font.Bold = true;
        ws1.Cell("A1").Style.Font.FontSize = 13;
        ws1.Cell("A3").Value = "Producto";
        ws1.Cell("B3").Value = "Unidades";
        ws1.Cell("C3").Value = "Ingreso (COP)";
        ws1.Row(3).Style.Font.Bold = true;
        for (int i = 0; i < topSellers.Count; i++)
        {
            ws1.Cell(4 + i, 1).Value = topSellers[i].ProductName;
            ws1.Cell(4 + i, 2).Value = topSellers[i].UnitsSold;
            ws1.Cell(4 + i, 3).Value = topSellers[i].TotalRevenue;
            ws1.Cell(4 + i, 3).Style.NumberFormat.Format = "#,##0";
        }
        ws1.Columns().AdjustToContents();

        // Sheet 2: Period Comparison
        var ws2 = workbook.Worksheets.Add("Comparacion");
        ws2.Cell("A1").Value = "Comparacion de Periodos";
        ws2.Cell("A1").Style.Font.Bold = true;
        ws2.Cell("A1").Style.Font.FontSize = 13;
        ws2.Cell("A3").Value = "Concepto"; ws2.Cell("B3").Value = "Valor";
        ws2.Row(3).Style.Font.Bold = true;
        var compRows = new[]
        {
            ("Ingreso Actual", comparison.CurrentRevenue),
            ("Ingreso Anterior", comparison.PriorRevenue),
            ("Costo Actual", comparison.CurrentCost),
            ("Costo Anterior", comparison.PriorCost),
            ("Margen Actual", comparison.CurrentMargin),
            ("Margen Anterior", comparison.PriorMargin),
            ("Crecimiento % Ingresos", comparison.RevenueGrowthPercent),
        };
        for (int i = 0; i < compRows.Length; i++)
        {
            ws2.Cell(4 + i, 1).Value = compRows[i].Item1;
            ws2.Cell(4 + i, 2).Value = compRows[i].Item2;
            ws2.Cell(4 + i, 2).Style.NumberFormat.Format = "#,##0.00";
        }
        ws2.Columns().AdjustToContents();

        // Sheet 3: Product Margins
        var ws3 = workbook.Worksheets.Add("Margenes");
        ws3.Cell("A1").Value = $"Margen por Producto — {from:yyyy-MM-dd} / {to:yyyy-MM-dd}";
        ws3.Cell("A1").Style.Font.Bold = true;
        ws3.Cell("A1").Style.Font.FontSize = 13;
        ws3.Cell("A3").Value = "Producto";
        ws3.Cell("B3").Value = "Ingreso";
        ws3.Cell("C3").Value = "Costo";
        ws3.Cell("D3").Value = "Margen";
        ws3.Cell("E3").Value = "Margen %";
        ws3.Row(3).Style.Font.Bold = true;
        for (int i = 0; i < margins.Count; i++)
        {
            var m = margins[i];
            ws3.Cell(4 + i, 1).Value = m.ProductName;
            ws3.Cell(4 + i, 2).Value = m.TotalRevenue;
            ws3.Cell(4 + i, 3).Value = m.TotalCost;
            ws3.Cell(4 + i, 4).Value = m.GrossMargin;
            ws3.Cell(4 + i, 5).Value = m.MarginPercent;
            for (int c = 2; c <= 5; c++)
                ws3.Cell(4 + i, c).Style.NumberFormat.Format = "#,##0.00";
        }
        ws3.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }
}
