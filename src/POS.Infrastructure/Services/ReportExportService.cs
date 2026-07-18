using System.Globalization;
using ClosedXML.Excel;
using POS.Application.DTOs.Reporting;
using POS.Application.DTOs.Shifts;
using POS.Application.Interfaces;
using POS.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POS.Infrastructure.Services;

public sealed class ReportExportService : IReportExportService
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    private const string BodyColor = "#1A1A1A";
    private const string LabelColor = "#6B6B6B";
    private const string BorderColor = "#D0D0D0";
    private const string HeaderFillColor = "#F2F2F2";
    private const string CurrencyFormat = "$ #,##0";
    private const string PercentageFormat = "0.0%";
    private const float ReportContentScale = 0.75f;

    // QuestPDF refuses to render without a license. ReportService sets this too, but it's never
    // instantiated in the running app, so PDF export here would throw unless we set it ourselves.
    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> ExportOperationalReportPdfAsync(
        OperationalReportDto report,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ct.ThrowIfCancellationRequested();

        string period = FormatPeriod(from, to);
        DateTimeOffset generatedAt = DateTimeOffset.Now;
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigureReportPage(page, "Reporte operacional", period, generatedAt);
                page.Content().Scale(ReportContentScale).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text("Resumen de recaudo y caja").FontSize(16).Bold();
                    column.Item().Text("Consulta primero los indicadores clave y luego usa el detalle para conciliar los valores.")
                        .FontSize(9).FontColor(LabelColor);
                    column.Item().Element(card => ComposeOperationalKpis(card, report));
                    column.Item().Element(section => ComposeOperationalBreakdown(section, report));
                    column.Item().Element(section => ComposeOperationalGuide(section));
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
        ArgumentNullException.ThrowIfNull(report);
        ct.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        string period = FormatPeriod(from, to);
        DateTimeOffset generatedAt = DateTimeOffset.Now;

        var summary = workbook.Worksheets.Add("Resumen");
        ConfigureWorksheetTitle(summary, 4, "Reporte operacional", period, generatedAt);
        AddOperationalSummary(summary, report);

        var reconciliation = workbook.Worksheets.Add("Conciliación de caja");
        ConfigureWorksheetTitle(reconciliation, 4, "Conciliación de caja", period, generatedAt);
        AddOperationalReconciliation(reconciliation, report);

        var detail = workbook.Worksheets.Add("Detalle de recaudo");
        ConfigureWorksheetTitle(detail, 6, "Detalle de recaudo", period, generatedAt);
        AddOperationalDetail(detail, report);

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
        ArgumentNullException.ThrowIfNull(topSellers);
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(margins);
        ct.ThrowIfCancellationRequested();

        string period = FormatPeriod(from, to);
        DateTimeOffset generatedAt = DateTimeOffset.Now;
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigureReportPage(page, "Reporte de rentabilidad", period, generatedAt);
                page.Content().Scale(ReportContentScale).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text("Panorama de rentabilidad").FontSize(16).Bold();
                    column.Item().Text("Los indicadores resumen el período; las tablas permiten identificar los productos que impulsan o reducen el margen.")
                        .FontSize(9).FontColor(LabelColor);
                    column.Item().Element(card => ComposeProfitKpis(card, topSellers, comparison, margins));
                    column.Item().Element(section => ComposeTopSellersTable(section, topSellers, comparison.CurrentRevenue));
                    column.Item().Element(section => ComposePeriodComparison(section, comparison));
                    column.Item().Element(section => ComposeMarginTable(section, margins));
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
        ArgumentNullException.ThrowIfNull(topSellers);
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(margins);
        ct.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        string period = FormatPeriod(from, to);
        DateTimeOffset generatedAt = DateTimeOffset.Now;

        var summary = workbook.Worksheets.Add("Resumen");
        ConfigureWorksheetTitle(summary, 6, "Reporte de rentabilidad", period, generatedAt);
        AddProfitSummary(summary, topSellers, comparison, margins);

        var topSellersSheet = workbook.Worksheets.Add("Top vendidos");
        ConfigureWorksheetTitle(topSellersSheet, 13, "Productos más vendidos", period, generatedAt);
        AddTopSellersSheet(topSellersSheet, topSellers, comparison.CurrentRevenue, margins);

        var marginsSheet = workbook.Worksheets.Add("Márgenes");
        ConfigureWorksheetTitle(marginsSheet, 6, "Margen por producto", period, generatedAt);
        AddMarginsSheet(marginsSheet, margins);

        var comparisonSheet = workbook.Worksheets.Add("Comparativo");
        ConfigureWorksheetTitle(comparisonSheet, 6, "Comparativo de períodos", period, generatedAt);
        AddPeriodComparisonSheet(comparisonSheet, comparison);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> ExportClosingsReportPdfAsync(IReadOnlyList<ShiftSummary> shifts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shifts);
        ct.ThrowIfCancellationRequested();

        string period = "Últimos cierres y arqueos de caja";
        DateTimeOffset generatedAt = DateTimeOffset.Now;
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigureReportPage(page, "Cierres y arqueos", period, generatedAt);
                page.Content().Scale(ReportContentScale).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text("Resumen de turnos y cierres de caja").FontSize(16).Bold();
                    column.Item().Text("A continuación se detallan los turnos cerrados recientemente y los turnos que aún están abiertos pendientes de arqueo.")
                        .FontSize(9).FontColor(LabelColor);

                    var closedShifts = shifts.Where(s => s.Status == ShiftStatus.Closed).OrderByDescending(s => s.ClosedAt).ToList();
                    if (closedShifts.Count > 0)
                    {
                        var lastClosed = closedShifts[0];
                        column.Item().Element(card => ComposeLastClosedShiftKpi(card, lastClosed));
                    }

                    column.Item().Element(section => ComposeClosedShiftsTable(section, closedShifts));

                    var openShifts = shifts.Where(s => s.Status == ShiftStatus.Open).OrderBy(s => s.OpenedAt).ToList();
                    column.Item().Element(section => ComposeOpenShiftsTable(section, openShifts));
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    public Task<byte[]> ExportClosingsReportExcelAsync(IReadOnlyList<ShiftSummary> shifts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shifts);
        ct.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        string period = "Últimos cierres y arqueos de caja";
        DateTimeOffset generatedAt = DateTimeOffset.Now;

        var closedSheet = workbook.Worksheets.Add("Cierres de Caja");
        ConfigureWorksheetTitle(closedSheet, 8, "Reporte de Cierres y Arqueos", period, generatedAt);
        var closedShifts = shifts.Where(s => s.Status == ShiftStatus.Closed).OrderByDescending(s => s.ClosedAt).ToList();
        AddExcelClosedShifts(closedSheet, closedShifts);

        var openSheet = workbook.Worksheets.Add("Turnos Abiertos");
        ConfigureWorksheetTitle(openSheet, 4, "Turnos Abiertos (Pendientes de Arqueo)", period, generatedAt);
        var openShifts = shifts.Where(s => s.Status == ShiftStatus.Open).OrderBy(s => s.OpenedAt).ToList();
        AddExcelOpenShifts(openSheet, openShifts);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    private static void ConfigureReportPage(PageDescriptor page, string title, string period, DateTimeOffset generatedAt)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.4f, Unit.Centimetre);
        page.DefaultTextStyle(text => text.FontFamily("Segoe UI").FontSize(10).FontColor(BodyColor));
        page.PageColor(Colors.White);
        page.Header().Element(header => ComposeReportHeader(header, title, period, generatedAt));
        page.Footer().Element(ComposeReportFooter);
    }

    private static void ComposeReportHeader(IContainer container, string title, string period, DateTimeOffset generatedAt)
    {
        container.PaddingBottom(12).BorderBottom(0.75f).BorderColor(BorderColor).Row(row =>
        {
            row.RelativeItem().Column(info =>
            {
                info.Spacing(2);
                info.Item().Text("POS LOCAL").FontSize(9).Bold().FontColor(LabelColor);
                info.Item().Text(title.ToUpperInvariant()).FontSize(20).Bold();
                info.Item().Text($"Período: {period}").FontSize(9).FontColor(LabelColor);
            });
            row.ConstantItem(155).AlignRight().Column(meta =>
            {
                meta.Spacing(2);
                meta.Item().Text("DOCUMENTO DE GESTIÓN").FontSize(8).Bold().FontColor(LabelColor);
                meta.Item().Text($"Generado: {generatedAt.ToLocalTime():yyyy-MM-dd HH:mm}").FontSize(8).FontColor(LabelColor);
            });
        });
    }

    private static void ComposeReportFooter(IContainer container)
    {
        container.PaddingTop(10).BorderTop(0.5f).BorderColor(BorderColor).Row(row =>
        {
            row.RelativeItem().Text("Generado por POS Local").FontSize(8).FontColor(LabelColor);
            row.ConstantItem(120).AlignRight().Text(text =>
            {
                text.Span("Página ").FontSize(8).FontColor(LabelColor);
                text.CurrentPageNumber().FontSize(8).FontColor(LabelColor);
            });
        });
    }

    private static void ComposeOperationalKpis(IContainer container, OperationalReportDto report)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Indicadores clave").FontSize(11).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                AddKpi(table, "Total facturado", report.BilledSalesTotal);
                AddKpi(table, "Efectivo de ventas", report.SaleCashCollected);
                AddKpi(table, "Pagos digitales", report.SaleDigitalCollected);
                AddKpi(table, "Abonos cobrados", report.ReceivableSettlementTotal);
                AddKpi(table, "Devoluciones en efectivo", report.CashReturnOutflows);
                AddKpi(table, "Devoluciones digitales", report.DigitalReturnOutflows);
                AddKpi(table, "Efectivo esperado", report.ExpectedDrawerCash);
                AddKpi(table, "Recaudo de ventas", report.SaleCashCollected + report.SaleDigitalCollected);
            });
        });
    }

    private static void ComposeOperationalBreakdown(IContainer container, OperationalReportDto report)
    {
        container.Element(CardStyle).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Detalle de recaudo y conciliación").FontSize(11).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(4);
                });
                AddTableHeader(table, "Grupo", "Concepto", "Monto", "Interpretación");
                AddOperationalRow(table, "Ventas", "Total facturado", report.BilledSalesTotal, "Valor de los artículos facturados en el período.");
                AddOperationalRow(table, "Ventas", "Efectivo recibido", report.SaleCashCollected, "Dinero físico ingresado por ventas.");
                AddOperationalRow(table, "Ventas", "Pagos digitales", report.SaleDigitalCollected, "Ingreso registrado fuera de la caja física.");
                AddOperationalRow(table, "Cartera", "Abonos en efectivo", report.ReceivableSettlementCash, "Abonos que aumentan el efectivo disponible.");
                AddOperationalRow(table, "Cartera", "Abonos digitales", report.ReceivableSettlementDigital, "Abonos registrados en canales digitales.");
                AddOperationalRow(table, "Devoluciones", "Efectivo entregado", report.CashReturnOutflows, "Salidas por devoluciones liquidadas en efectivo.");
                AddOperationalRow(table, "Devoluciones", "Digital entregado", report.DigitalReturnOutflows, "Salidas por devoluciones liquidadas en canales digitales.");
                AddOperationalRow(table, "Caja", "Efectivo esperado", report.ExpectedDrawerCash, "Monto físico que debe conciliase contra el efectivo contado.", isEmphasized: true);
            });
        });
    }

    private static void ComposeOperationalGuide(IContainer container)
    {
        container.Element(CardStyle).Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("Cómo leer este reporte").FontSize(10).Bold().FontColor(LabelColor);
            column.Item().Text("El efectivo esperado incluye efectivo de ventas y abonos en efectivo. Los pagos digitales son ingresos, pero no deben estar en el cajón de caja.")
                .FontSize(9);
        });
    }

    private static void ComposeProfitKpis(
        IContainer container,
        IReadOnlyList<TopSellerResult> topSellers,
        PeriodComparisonResult comparison,
        IReadOnlyList<ProductMarginResult> margins)
    {
        decimal marginPercentage = comparison.CurrentRevenue == 0m
            ? 0m
            : comparison.CurrentMargin / comparison.CurrentRevenue * 100m;

        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Indicadores clave").FontSize(11).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                AddKpi(table, "Ingresos", comparison.CurrentRevenue);
                AddKpi(table, "Costos", comparison.CurrentCost);
                AddKpi(table, "Margen bruto", comparison.CurrentMargin);
                AddKpi(table, "Margen sobre ventas", $"{marginPercentage.ToString("N1", Co)}%");
                AddKpi(table, "Crecimiento ingresos", $"{comparison.RevenueGrowthPercent.ToString("N1", Co)}%");
                AddKpi(table, "Productos analizados", margins.Count.ToString("N0", Co));
            });

            if (topSellers.Count > 0)
            {
                TopSellerResult topSeller = topSellers[0];
                column.Item().Text($"Producto líder: {topSeller.ProductName} · {topSeller.UnitsSold.ToString("N0", Co)} unidades vendidas")
                    .FontSize(9).FontColor(LabelColor);
            }
        });
    }

    private static void ComposeTopSellersTable(IContainer container, IReadOnlyList<TopSellerResult> topSellers, decimal totalRevenue)
    {
        container.Element(CardStyle).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Productos más vendidos").FontSize(11).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(34);
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.2f);
                });
                AddTableHeader(table, "#", "Producto", "Unid.", "Ingreso", "% ventas");

                if (topSellers.Count == 0)
                {
                    AddEmptyTableRow(table, 5, "Sin productos vendidos en el período seleccionado.");
                    return;
                }

                for (int index = 0; index < topSellers.Count; index++)
                {
                    TopSellerResult seller = topSellers[index];
                    decimal share = totalRevenue == 0m ? 0m : seller.TotalRevenue / totalRevenue * 100m;
                    table.Cell().Element(CellStyle).AlignCenter().Text((index + 1).ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(CellStyle).Text(seller.ProductName).Bold();
                    table.Cell().Element(CellStyle).AlignRight().Text(seller.UnitsSold.ToString("N0", Co));
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(seller.TotalRevenue));
                    table.Cell().Element(CellStyle).AlignRight().Text($"{share.ToString("N1", Co)}%");
                }
            });
        });
    }

    private static void ComposePeriodComparison(IContainer container, PeriodComparisonResult comparison)
    {
        container.Element(CardStyle).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Comparación con el período anterior").FontSize(11).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });
                AddTableHeader(table, "Indicador", "Actual", "Anterior", "Variación");
                AddComparisonRow(table, "Ingresos", comparison.CurrentRevenue, comparison.PriorRevenue, comparison.RevenueGrowthPercent);
                AddComparisonRow(table, "Costos", comparison.CurrentCost, comparison.PriorCost, PercentChange(comparison.CurrentCost, comparison.PriorCost));
                AddComparisonRow(table, "Margen bruto", comparison.CurrentMargin, comparison.PriorMargin, PercentChange(comparison.CurrentMargin, comparison.PriorMargin));
            });
        });
    }

    private static void ComposeMarginTable(IContainer container, IReadOnlyList<ProductMarginResult> margins)
    {
        container.Element(CardStyle).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Margen por producto").FontSize(11).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(1.2f);
                });
                AddTableHeader(table, "Producto", "Ingreso", "Costo", "Margen", "% margen");

                if (margins.Count == 0)
                {
                    AddEmptyTableRow(table, 5, "Sin margen por producto para el período seleccionado.");
                    return;
                }

                foreach (ProductMarginResult margin in margins)
                {
                    table.Cell().Element(CellStyle).Text(margin.ProductName).Bold();
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(margin.TotalRevenue));
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(margin.TotalCost));
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(margin.GrossMargin));
                    table.Cell().Element(CellStyle).AlignRight().Text($"{margin.MarginPercent.ToString("N1", Co)}%");
                }
            });
        });
    }

    private static void AddKpi(TableDescriptor table, string label, decimal value) => AddKpi(table, label, FormatCurrency(value));

    private static void AddKpi(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingRight(4).Element(KpiCard).Column(column =>
        {
            column.Spacing(3);
            column.Item().Text(label).FontSize(8).FontColor(LabelColor);
            column.Item().Text(value).FontSize(12).Bold();
        });
    }

    private static void AddTableHeader(TableDescriptor table, params string[] headers)
    {
        table.Header(headerRow =>
        {
            foreach (string header in headers)
            {
                headerRow.Cell().Element(HeaderCellStyle).Text(header).Bold();
            }
        });
    }

    private static void AddOperationalRow(
        TableDescriptor table,
        string group,
        string concept,
        decimal amount,
        string interpretation,
        bool isEmphasized = false)
    {
        table.Cell().Element(container => DataCellStyle(container, isEmphasized)).Text(group);
        table.Cell().Element(container => DataCellStyle(container, isEmphasized)).Text(concept).Bold();
        table.Cell().Element(container => DataCellStyle(container, isEmphasized)).AlignRight().Text(FormatCurrency(amount)).Bold();
        table.Cell().Element(container => DataCellStyle(container, isEmphasized)).Text(interpretation).FontSize(8).FontColor(LabelColor);
    }

    private static void AddComparisonRow(TableDescriptor table, string label, decimal current, decimal prior, decimal percentage)
    {
        string variation = prior == 0m && current != 0m ? "N/A" : $"{percentage.ToString("N1", Co)}%";
        table.Cell().Element(CellStyle).Text(label).Bold();
        table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(current));
        table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(prior));
        table.Cell().Element(CellStyle).AlignRight().Text(variation).Bold();
    }

    private static void AddEmptyTableRow(TableDescriptor table, int columnCount, string message)
    {
        table.Cell().ColumnSpan((uint)columnCount).Element(CellStyle).AlignCenter().Text(message).Italic().FontColor(LabelColor);
    }

    private static void ConfigureWorksheetTitle(IXLWorksheet worksheet, int lastColumn, string title, string period, DateTimeOffset generatedAt)
    {
        worksheet.Range(1, 1, 1, lastColumn).Merge();
        worksheet.Cell(1, 1).Value = title.ToUpperInvariant();
        worksheet.Range(1, 1, 1, lastColumn).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, lastColumn).Style.Font.FontSize = 16;
        worksheet.Range(1, 1, 1, lastColumn).Style.Font.FontColor = XLColor.White;
        worksheet.Range(1, 1, 1, lastColumn).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        worksheet.Range(1, 1, 1, lastColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(1).Height = 28;

        worksheet.Range(2, 1, 2, lastColumn).Merge();
        worksheet.Cell(2, 1).Value = $"Período: {period}  |  Generado: {generatedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        worksheet.Range(2, 1, 2, lastColumn).Style.Font.FontColor = XLColor.DimGray;
        worksheet.Range(2, 1, 2, lastColumn).Style.Font.Italic = true;
        worksheet.Range(2, 1, 2, lastColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void AddOperationalSummary(IXLWorksheet worksheet, OperationalReportDto report)
    {
        AddExcelSectionTitle(worksheet, 4, 1, 4, "Indicadores clave");
        AddExcelKpi(worksheet, 5, 1, "Total facturado", report.BilledSalesTotal);
        AddExcelKpi(worksheet, 5, 3, "Efectivo esperado", report.ExpectedDrawerCash);
        AddExcelKpi(worksheet, 7, 1, "Pagos digitales", report.SaleDigitalCollected);
        AddExcelKpi(worksheet, 7, 3, "Abonos cobrados", report.ReceivableSettlementTotal);

        AddExcelSectionTitle(worksheet, 10, 1, 4, "Resumen de recaudo");
        AddExcelHeader(worksheet, 11, "Grupo", "Concepto", "Monto (COP)", "Qué representa");
        AddExcelOperationalRow(worksheet, 12, "Ventas", "Total facturado", report.BilledSalesTotal, "Valor de los artículos facturados.");
        AddExcelOperationalRow(worksheet, 13, "Ventas", "Efectivo recibido", report.SaleCashCollected, "Dinero físico ingresado por ventas.");
        AddExcelOperationalRow(worksheet, 14, "Ventas", "Pagos digitales", report.SaleDigitalCollected, "Ingreso fuera de la caja física.");
        AddExcelOperationalRow(worksheet, 15, "Cartera", "Abonos cobrados", report.ReceivableSettlementTotal, "Pagos aplicados a cuentas por cobrar.");
        AddExcelOperationalRow(worksheet, 16, "Caja", "Efectivo esperado", report.ExpectedDrawerCash, "Monto que debe conciliase contra el efectivo contado.", isEmphasized: true);
        AddExcelOperationalRow(worksheet, 17, "Devoluciones", "Devoluciones en efectivo", report.CashReturnOutflows, "Salidas liquidadas en efectivo.");
        AddExcelOperationalRow(worksheet, 18, "Devoluciones", "Devoluciones digitales", report.DigitalReturnOutflows, "Salidas liquidadas en canales digitales.");
        FormatExcelTable(worksheet, 11, 18, 4, freezeRows: 11);
        worksheet.Column(1).Width = 16;
        worksheet.Column(2).Width = 25;
        worksheet.Column(3).Width = 18;
        worksheet.Column(4).Width = 46;
    }

    private static void AddOperationalReconciliation(IXLWorksheet worksheet, OperationalReportDto report)
    {
        AddExcelSectionTitle(worksheet, 4, 1, 4, "Entradas que afectan la conciliación");
        AddExcelHeader(worksheet, 5, "Origen", "Canal", "Monto (COP)", "Impacto en caja física");
        AddExcelOperationalRow(worksheet, 6, "Ventas", "Efectivo", report.SaleCashCollected, "Sí, aumenta el efectivo esperado.");
        AddExcelOperationalRow(worksheet, 7, "Ventas", "Digital", report.SaleDigitalCollected, "No, se recauda fuera de la caja.");
        AddExcelOperationalRow(worksheet, 8, "Abonos de cartera", "Efectivo", report.ReceivableSettlementCash, "Sí, aumenta el efectivo esperado.");
        AddExcelOperationalRow(worksheet, 9, "Abonos de cartera", "Digital", report.ReceivableSettlementDigital, "No, se recauda fuera de la caja.");
        AddExcelOperationalRow(worksheet, 10, "Devoluciones", "Efectivo", report.CashReturnOutflows, "Sí, representa una salida de caja física.");
        AddExcelOperationalRow(worksheet, 11, "Devoluciones", "Digital", report.DigitalReturnOutflows, "No, se liquida fuera de la caja.");
        AddExcelOperationalRow(worksheet, 12, "Conciliación", "Efectivo esperado", report.ExpectedDrawerCash, "Compare este valor con el dinero contado.", isEmphasized: true);
        FormatExcelTable(worksheet, 5, 12, 4, freezeRows: 5);
        worksheet.Column(1).Width = 22;
        worksheet.Column(2).Width = 22;
        worksheet.Column(3).Width = 18;
        worksheet.Column(4).Width = 42;
    }

    private static void AddOperationalDetail(IXLWorksheet worksheet, OperationalReportDto report)
    {
        AddExcelSectionTitle(worksheet, 4, 1, 6, "Componentes del recaudo");
        AddExcelHeader(worksheet, 5, "Grupo", "Concepto", "Monto (COP)", "Cálculo", "Caja física", "Uso operativo");
        AddExcelDetailedOperationalRow(worksheet, 6, "Ventas", "Total facturado", report.BilledSalesTotal, "Suma de artículos facturados", "No aplica", "Referencia comercial del período.");
        AddExcelDetailedOperationalRow(worksheet, 7, "Ventas", "Recaudo de ventas", report.SaleCashCollected + report.SaleDigitalCollected, "Efectivo de ventas + pagos digitales", "Parcial", "Dinero efectivamente cobrado por ventas.");
        AddExcelDetailedOperationalRow(worksheet, 8, "Ventas", "Efectivo de ventas", report.SaleCashCollected, "Cobros de venta en efectivo", "Sí", "Aumenta el efectivo esperado.");
        AddExcelDetailedOperationalRow(worksheet, 9, "Ventas", "Pagos digitales", report.SaleDigitalCollected, "Cobros de venta digitales", "No", "Ingreso conciliado fuera del cajón.");
        AddExcelDetailedOperationalRow(worksheet, 10, "Cartera", "Abonos cobrados", report.ReceivableSettlementTotal, "Abonos en efectivo + abonos digitales", "Parcial", "Recuperación de cuentas por cobrar.");
        AddExcelDetailedOperationalRow(worksheet, 11, "Cartera", "Abonos en efectivo", report.ReceivableSettlementCash, "Pagos de cartera en efectivo", "Sí", "Aumenta el efectivo esperado.");
        AddExcelDetailedOperationalRow(worksheet, 12, "Cartera", "Abonos digitales", report.ReceivableSettlementDigital, "Pagos de cartera digitales", "No", "Ingreso conciliado fuera del cajón.");
        AddExcelDetailedOperationalRow(worksheet, 13, "Caja", "Efectivo esperado", report.ExpectedDrawerCash, "Efectivo de ventas + abonos en efectivo", "Sí", "Compare este total contra el efectivo contado.", isEmphasized: true);
        AddExcelDetailedOperationalRow(worksheet, 14, "Recaudo", "Total cobrado", report.SaleCashCollected + report.SaleDigitalCollected + report.ReceivableSettlementTotal, "Recaudo de ventas + abonos cobrados", "Mixto", "Total de entradas cobradas, físicas y digitales.", isEmphasized: true);
        AddExcelDetailedOperationalRow(worksheet, 15, "Devoluciones", "Efectivo entregado", report.CashReturnOutflows, "Devoluciones liquidadas en efectivo", "Sí", "Salida física registrada en el período.");
        AddExcelDetailedOperationalRow(worksheet, 16, "Devoluciones", "Digital entregado", report.DigitalReturnOutflows, "Devoluciones liquidadas digitalmente", "No", "Salida digital registrada en el período.");
        FormatExcelTable(worksheet, 5, 16, 6, freezeRows: 5);
        worksheet.Column(1).Width = 15;
        worksheet.Column(2).Width = 24;
        worksheet.Column(3).Width = 18;
        worksheet.Column(4).Width = 34;
        worksheet.Column(5).Width = 14;
        worksheet.Column(6).Width = 40;
    }

    private static void AddProfitSummary(
        IXLWorksheet worksheet,
        IReadOnlyList<TopSellerResult> topSellers,
        PeriodComparisonResult comparison,
        IReadOnlyList<ProductMarginResult> margins)
    {
        decimal marginPercentage = comparison.CurrentRevenue == 0m ? 0m : comparison.CurrentMargin / comparison.CurrentRevenue;
        AddExcelSectionTitle(worksheet, 4, 1, 6, "Indicadores clave");
        AddExcelKpi(worksheet, 5, 1, "Ingresos", comparison.CurrentRevenue);
        AddExcelKpi(worksheet, 5, 3, "Costos", comparison.CurrentCost);
        AddExcelKpi(worksheet, 5, 5, "Margen bruto", comparison.CurrentMargin);
        AddExcelKpi(worksheet, 7, 1, "Margen sobre ventas", marginPercentage, PercentageFormat);
        AddExcelKpi(worksheet, 7, 3, "Crecimiento ingresos", comparison.RevenueGrowthPercent / 100m, PercentageFormat);
        AddExcelKpi(worksheet, 7, 5, "Productos analizados", margins.Count);

        AddExcelSectionTitle(worksheet, 10, 1, 6, "Comparación con el período anterior");
        AddExcelHeader(worksheet, 11, "Indicador", "Actual", "Anterior", "Variación", "Lectura", "");
        AddExcelComparisonRow(worksheet, 12, "Ingresos", comparison.CurrentRevenue, comparison.PriorRevenue, comparison.RevenueGrowthPercent);
        AddExcelComparisonRow(worksheet, 13, "Costos", comparison.CurrentCost, comparison.PriorCost, PercentChange(comparison.CurrentCost, comparison.PriorCost));
        AddExcelComparisonRow(worksheet, 14, "Margen bruto", comparison.CurrentMargin, comparison.PriorMargin, PercentChange(comparison.CurrentMargin, comparison.PriorMargin));
        FormatExcelTable(worksheet, 11, 14, 5, freezeRows: 11);

        string topProduct = topSellers.Count == 0
            ? "Sin ventas en el período seleccionado."
            : $"Producto líder: {topSellers[0].ProductName} ({topSellers[0].UnitsSold.ToString("N0", Co)} unidades).";
        worksheet.Range(17, 1, 17, 6).Merge();
        worksheet.Cell(17, 1).Value = topProduct;
        worksheet.Range(17, 1, 17, 6).Style.Font.Italic = true;
        worksheet.Range(17, 1, 17, 6).Style.Font.FontColor = XLColor.DimGray;
        worksheet.Column(1).Width = 24;
        worksheet.Columns(2, 4).Width = 17;
        worksheet.Columns(5, 6).Width = 20;
    }

    private static void AddTopSellersSheet(
        IXLWorksheet worksheet,
        IReadOnlyList<TopSellerResult> topSellers,
        decimal totalRevenue,
        IReadOnlyList<ProductMarginResult> margins)
    {
        var marginMap = margins.ToDictionary(m => m.ProductId);

        AddExcelHeader(worksheet, 4,
            "Ranking",
            "Producto",
            "Transacciones",
            "Compradores Únicos",
            "Unidades Vendidas",
            "Precio Prom. (COP)",
            "Costo Prom. (COP)",
            "Margen Prom. (COP)",
            "Ingreso Total (COP)",
            "Costo Total (COP)",
            "Margen Total (COP)",
            "Ratio Margen %",
            "% de Ventas Totales");

        if (topSellers.Count == 0)
        {
            worksheet.Range(5, 1, 5, 13).Merge();
            worksheet.Cell(5, 1).Value = "Sin productos vendidos en el período seleccionado.";
            worksheet.Cell(5, 1).Style.Font.Italic = true;
            worksheet.Cell(5, 1).Style.Font.FontColor = XLColor.DimGray;
        }
        else
        {
            for (int index = 0; index < topSellers.Count; index++)
            {
                TopSellerResult seller = topSellers[index];
                int row = 5 + index;

                decimal totalCost = 0m;
                decimal grossMargin = 0m;
                decimal marginPercent = 0m;
                if (marginMap.TryGetValue(seller.ProductId, out var margin))
                {
                    totalCost = margin.TotalCost;
                    grossMargin = margin.GrossMargin;
                    marginPercent = margin.MarginPercent / 100m;
                }

                decimal avgPrice = seller.UnitsSold > 0 ? seller.TotalRevenue / seller.UnitsSold : 0m;
                decimal avgCost = seller.UnitsSold > 0 ? totalCost / seller.UnitsSold : 0m;
                decimal avgMargin = seller.UnitsSold > 0 ? grossMargin / seller.UnitsSold : 0m;

                worksheet.Cell(row, 1).Value = index + 1;
                worksheet.Cell(row, 2).Value = seller.ProductName;
                worksheet.Cell(row, 3).Value = seller.TransactionCount;
                worksheet.Cell(row, 4).Value = seller.UniqueCustomersCount;
                worksheet.Cell(row, 5).Value = seller.UnitsSold;
                worksheet.Cell(row, 6).Value = avgPrice;
                worksheet.Cell(row, 7).Value = avgCost;
                worksheet.Cell(row, 8).Value = avgMargin;
                worksheet.Cell(row, 9).Value = seller.TotalRevenue;
                worksheet.Cell(row, 10).Value = totalCost;
                worksheet.Cell(row, 11).Value = grossMargin;
                worksheet.Cell(row, 12).Value = marginPercent;
                worksheet.Cell(row, 13).Value = totalRevenue == 0m ? 0m : seller.TotalRevenue / totalRevenue;

                worksheet.Range(row, 6, row, 11).Style.NumberFormat.Format = CurrencyFormat;
                worksheet.Cell(row, 12).Style.NumberFormat.Format = PercentageFormat;
                worksheet.Cell(row, 13).Style.NumberFormat.Format = PercentageFormat;
            }
        }

        int lastRow = Math.Max(5, 4 + topSellers.Count);
        FormatExcelTable(worksheet, 4, lastRow, 13, freezeRows: 4);
        worksheet.Column(1).Width = 10;
        worksheet.Column(2).Width = 38;
        worksheet.Column(3).Width = 16;
        worksheet.Column(4).Width = 20;
        worksheet.Column(5).Width = 18;
        worksheet.Column(6).Width = 18;
        worksheet.Column(7).Width = 18;
        worksheet.Column(8).Width = 18;
        worksheet.Column(9).Width = 20;
        worksheet.Column(10).Width = 20;
        worksheet.Column(11).Width = 20;
        worksheet.Column(12).Width = 16;
        worksheet.Column(13).Width = 20;
    }

    private static void AddMarginsSheet(IXLWorksheet worksheet, IReadOnlyList<ProductMarginResult> margins)
    {
        AddExcelHeader(worksheet, 4, "Producto", "Ingreso (COP)", "Costo (COP)", "Margen (COP)", "% margen", "Estado");
        if (margins.Count == 0)
        {
            worksheet.Range(5, 1, 5, 6).Merge();
            worksheet.Cell(5, 1).Value = "Sin margen por producto para el período seleccionado.";
            worksheet.Cell(5, 1).Style.Font.Italic = true;
            worksheet.Cell(5, 1).Style.Font.FontColor = XLColor.DimGray;
        }
        else
        {
            for (int index = 0; index < margins.Count; index++)
            {
                ProductMarginResult margin = margins[index];
                int row = 5 + index;
                worksheet.Cell(row, 1).Value = margin.ProductName;
                worksheet.Cell(row, 2).Value = margin.TotalRevenue;
                worksheet.Cell(row, 3).Value = margin.TotalCost;
                worksheet.Cell(row, 4).Value = margin.GrossMargin;
                worksheet.Cell(row, 5).Value = margin.MarginPercent / 100m;
                worksheet.Cell(row, 6).Value = margin.GrossMargin >= 0m ? "Rentable" : "Revisar";
                worksheet.Range(row, 2, row, 4).Style.NumberFormat.Format = CurrencyFormat;
                worksheet.Cell(row, 5).Style.NumberFormat.Format = PercentageFormat;
                worksheet.Cell(row, 6).Style.Font.FontColor = margin.GrossMargin >= 0m ? XLColor.DarkGreen : XLColor.DarkRed;
            }
        }

        int lastRow = Math.Max(5, 4 + margins.Count);
        FormatExcelTable(worksheet, 4, lastRow, 6, freezeRows: 4);
        worksheet.Column(1).Width = 38;
        worksheet.Columns(2, 4).Width = 18;
        worksheet.Column(5).Width = 14;
        worksheet.Column(6).Width = 14;
    }

    private static void AddPeriodComparisonSheet(IXLWorksheet worksheet, PeriodComparisonResult comparison)
    {
        AddExcelSectionTitle(worksheet, 4, 1, 6, "Evolución frente al período anterior");
        AddExcelHeader(worksheet, 5, "Indicador", "Actual (COP)", "Anterior (COP)", "Variación (COP)", "Variación (%)", "Lectura");
        AddExcelDetailedComparisonRow(worksheet, 6, "Ingresos", comparison.CurrentRevenue, comparison.PriorRevenue, comparison.RevenueGrowthPercent);
        AddExcelDetailedComparisonRow(worksheet, 7, "Costos", comparison.CurrentCost, comparison.PriorCost, PercentChange(comparison.CurrentCost, comparison.PriorCost));
        AddExcelDetailedComparisonRow(worksheet, 8, "Margen bruto", comparison.CurrentMargin, comparison.PriorMargin, PercentChange(comparison.CurrentMargin, comparison.PriorMargin));
        FormatExcelTable(worksheet, 5, 8, 6, freezeRows: 5);
        worksheet.Column(1).Width = 22;
        worksheet.Columns(2, 4).Width = 18;
        worksheet.Column(5).Width = 16;
        worksheet.Column(6).Width = 24;
    }

    private static void AddExcelSectionTitle(IXLWorksheet worksheet, int row, int firstColumn, int lastColumn, string title)
    {
        worksheet.Range(row, firstColumn, row, lastColumn).Merge();
        worksheet.Cell(row, firstColumn).Value = title;
        worksheet.Range(row, firstColumn, row, lastColumn).Style.Font.Bold = true;
        worksheet.Range(row, firstColumn, row, lastColumn).Style.Font.FontColor = XLColor.White;
        worksheet.Range(row, firstColumn, row, lastColumn).Style.Fill.BackgroundColor = XLColor.FromHtml("#5B9BD5");
    }

    private static void AddExcelKpi(IXLWorksheet worksheet, int row, int column, string label, decimal value, string numberFormat = CurrencyFormat)
    {
        worksheet.Range(row, column, row, column + 1).Merge();
        worksheet.Cell(row, column).Value = label;
        worksheet.Range(row, column, row, column + 1).Style.Font.Bold = true;
        worksheet.Range(row, column, row, column + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");
        worksheet.Range(row + 1, column, row + 1, column + 1).Merge();
        worksheet.Cell(row + 1, column).Value = value;
        worksheet.Cell(row + 1, column).Style.Font.Bold = true;
        worksheet.Cell(row + 1, column).Style.Font.FontSize = 13;
        worksheet.Cell(row + 1, column).Style.NumberFormat.Format = numberFormat;
        worksheet.Range(row, column, row + 1, column + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(row, column, row + 1, column + 1).Style.Border.OutsideBorderColor = XLColor.LightGray;
    }

    private static void AddExcelKpi(IXLWorksheet worksheet, int row, int column, string label, int value)
    {
        AddExcelKpi(worksheet, row, column, label, (decimal)value, "#,##0");
    }

    private static void AddExcelHeader(IXLWorksheet worksheet, int row, params string[] headers)
    {
        for (int index = 0; index < headers.Length; index++)
        {
            IXLCell cell = worksheet.Cell(row, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    private static void AddExcelOperationalRow(
        IXLWorksheet worksheet,
        int row,
        string group,
        string concept,
        decimal amount,
        string interpretation,
        bool isEmphasized = false)
    {
        worksheet.Cell(row, 1).Value = group;
        worksheet.Cell(row, 2).Value = concept;
        worksheet.Cell(row, 3).Value = amount;
        worksheet.Cell(row, 3).Style.NumberFormat.Format = CurrencyFormat;
        worksheet.Cell(row, 4).Value = interpretation;

        if (isEmphasized)
        {
            worksheet.Range(row, 1, row, 4).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2F0D9");
        }
    }

    private static void AddExcelDetailedOperationalRow(
        IXLWorksheet worksheet,
        int row,
        string group,
        string concept,
        decimal amount,
        string calculation,
        string cashImpact,
        string operationalUse,
        bool isEmphasized = false)
    {
        worksheet.Cell(row, 1).Value = group;
        worksheet.Cell(row, 2).Value = concept;
        worksheet.Cell(row, 3).Value = amount;
        worksheet.Cell(row, 3).Style.NumberFormat.Format = CurrencyFormat;
        worksheet.Cell(row, 4).Value = calculation;
        worksheet.Cell(row, 5).Value = cashImpact;
        worksheet.Cell(row, 6).Value = operationalUse;

        if (isEmphasized)
        {
            worksheet.Range(row, 1, row, 6).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2F0D9");
        }
    }

    private static void AddExcelComparisonRow(IXLWorksheet worksheet, int row, string label, decimal current, decimal prior, decimal percentage)
    {
        worksheet.Cell(row, 1).Value = label;
        worksheet.Cell(row, 2).Value = current;
        worksheet.Cell(row, 3).Value = prior;
        worksheet.Cell(row, 4).Value = percentage / 100m;
        worksheet.Cell(row, 5).Value = prior == 0m && current != 0m
            ? "Sin base anterior"
            : percentage >= 0m ? "Mejora" : "Disminuye";
        worksheet.Range(row, 2, row, 3).Style.NumberFormat.Format = CurrencyFormat;
        worksheet.Cell(row, 4).Style.NumberFormat.Format = PercentageFormat;
        worksheet.Cell(row, 5).Style.Font.FontColor = percentage >= 0m ? XLColor.DarkGreen : XLColor.DarkRed;
    }

    private static void AddExcelDetailedComparisonRow(IXLWorksheet worksheet, int row, string label, decimal current, decimal prior, decimal percentage)
    {
        decimal difference = current - prior;
        worksheet.Cell(row, 1).Value = label;
        worksheet.Cell(row, 2).Value = current;
        worksheet.Cell(row, 3).Value = prior;
        worksheet.Cell(row, 4).Value = difference;
        worksheet.Cell(row, 5).Value = percentage / 100m;
        worksheet.Cell(row, 6).Value = prior == 0m && current != 0m
            ? "Sin base anterior"
            : percentage >= 0m ? "Aumentó" : "Disminuyó";
        worksheet.Range(row, 2, row, 4).Style.NumberFormat.Format = CurrencyFormat;
        worksheet.Cell(row, 5).Style.NumberFormat.Format = PercentageFormat;
        worksheet.Range(row, 4, row, 6).Style.Font.FontColor = percentage >= 0m ? XLColor.DarkGreen : XLColor.DarkRed;
    }

    private static void FormatExcelTable(IXLWorksheet worksheet, int headerRow, int lastRow, int lastColumn, int freezeRows)
    {
        IXLRange range = worksheet.Range(headerRow, 1, lastRow, lastColumn);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        range.Style.Border.OutsideBorderColor = XLColor.LightGray;
        range.Style.Border.InsideBorderColor = XLColor.LightGray;
        worksheet.Range(headerRow, 1, headerRow, lastColumn).SetAutoFilter();
        worksheet.SheetView.FreezeRows(freezeRows);
    }

    private static string FormatPeriod(DateTimeOffset from, DateTimeOffset to) =>
        $"{from.ToLocalTime():yyyy-MM-dd HH:mm} — {to.ToLocalTime():yyyy-MM-dd HH:mm}";

    private static string FormatPeriod(DateOnly from, DateOnly to) => $"{from:yyyy-MM-dd} — {to:yyyy-MM-dd}";

    private static string FormatCurrency(decimal value) => value.ToString("C0", Co);

    private static decimal PercentChange(decimal current, decimal prior) => prior == 0m ? 0m : (current - prior) / prior * 100m;

    private static IContainer CardStyle(IContainer container) =>
        container.Border(0.75f).BorderColor(BorderColor).Padding(12);

    private static IContainer KpiCard(IContainer container) =>
        container.Border(0.75f).BorderColor(BorderColor).Padding(8);

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.Background(HeaderFillColor).BorderBottom(0.75f).BorderColor(BorderColor).PaddingVertical(6).PaddingHorizontal(6);

    private static IContainer CellStyle(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor("#ECECEC").PaddingVertical(6).PaddingHorizontal(6);

    private static IContainer DataCellStyle(IContainer container, bool isEmphasized) =>
        (isEmphasized ? container.Background("#F2F2F2") : container)
            .BorderBottom(0.5f).BorderColor("#ECECEC").PaddingVertical(6).PaddingHorizontal(6);

    private static void ComposeLastClosedShiftKpi(IContainer container, ShiftSummary s)
    {
        var diff = (s.ClosingCash ?? 0m) - s.ExpectedCash;
        string diffText = diff == 0 ? "Cuadra" : (diff > 0 ? $"+{FormatCurrency(diff)}" : $"-{FormatCurrency(Math.Abs(diff))}");

        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Último cierre registrado").FontSize(11).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                AddKpi(table, "Cajero", s.CashierUsername);
                AddKpi(table, "Fecha de Cierre", s.ClosedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—");
                AddKpi(table, "Efectivo Entregado", s.ClosingCash ?? 0m);
                AddKpi(table, "Diferencia", diffText);
            });
        });
    }

    private static void ComposeClosedShiftsTable(IContainer container, IReadOnlyList<ShiftSummary> closedShifts)
    {
        container.Element(CardStyle).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Cierres de Caja (Últimos 10)").FontSize(11).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                });
                AddTableHeader(table, "Cajero", "Apertura", "Cierre", "Base", "Ventas", "Esperado", "Entregado", "Diferencia");

                if (closedShifts.Count == 0)
                {
                    AddEmptyTableRow(table, 8, "Sin cierres de caja registrados.");
                    return;
                }

                foreach (var s in closedShifts)
                {
                    var diff = (s.ClosingCash ?? 0m) - s.ExpectedCash;
                    string diffText = diff == 0 ? "Cuadra" : (diff > 0 ? $"+{FormatCurrency(diff)}" : $"-{FormatCurrency(Math.Abs(diff))}");

                    table.Cell().Element(CellStyle).Text(s.CashierUsername).Bold();
                    table.Cell().Element(CellStyle).Text(s.OpenedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
                    table.Cell().Element(CellStyle).Text(s.ClosedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—");
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(s.OpeningCash));
                    table.Cell().Element(CellStyle).AlignRight().Text(s.HasClosingSummary ? FormatCurrency(s.SalesTotal) : "—");
                    table.Cell().Element(CellStyle).AlignRight().Text(s.HasClosingSummary ? FormatCurrency(s.ExpectedCash) : "—");
                    table.Cell().Element(CellStyle).AlignRight().Text(s.ClosingCash.HasValue ? FormatCurrency(s.ClosingCash.Value) : "—");

                    var diffCell = table.Cell().Element(CellStyle).AlignRight();
                    if (diff == 0)
                        diffCell.Text(diffText).FontColor("#2E7D32");
                    else if (diff > 0)
                        diffCell.Text(diffText).Bold().FontColor("#1565C0");
                    else
                        diffCell.Text(diffText).Bold().FontColor("#C62828");
                }
            });
        });
    }

    private static void ComposeOpenShiftsTable(IContainer container, IReadOnlyList<ShiftSummary> openShifts)
    {
        container.Element(CardStyle).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Turnos Abiertos / Pendientes de Arqueo").FontSize(11).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(3f);
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(3f);
                });
                AddTableHeader(table, "Cajero", "Fecha de Apertura", "Base de Apertura", "Estado");

                if (openShifts.Count == 0)
                {
                    AddEmptyTableRow(table, 4, "No hay turnos abiertos en este momento.");
                    return;
                }

                foreach (var s in openShifts)
                {
                    table.Cell().Element(CellStyle).Text(s.CashierUsername).Bold();
                    table.Cell().Element(CellStyle).Text(s.OpenedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(s.OpeningCash));
                    table.Cell().Element(CellStyle).Text("Abierto (Activo)").FontColor("#2E7D32").Bold();
                }
            });
        });
    }

    private static void AddExcelClosedShifts(IXLWorksheet worksheet, IReadOnlyList<ShiftSummary> closedShifts)
    {
        AddExcelSectionTitle(worksheet, 4, 1, 8, "Cierres de Caja Registrados");
        AddExcelHeader(worksheet, 5, "Cajero", "Apertura", "Cierre", "Base de Apertura", "Ventas en Efectivo", "Efectivo Esperado", "Efectivo Entregado", "Diferencia");

        int currentRow = 6;
        if (closedShifts.Count == 0)
        {
            worksheet.Range(currentRow, 1, currentRow, 8).Merge();
            worksheet.Cell(currentRow, 1).Value = "Sin cierres de caja registrados.";
            worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            currentRow++;
        }
        else
        {
            foreach (var s in closedShifts)
            {
                var diff = (s.ClosingCash ?? 0m) - s.ExpectedCash;

                worksheet.Cell(currentRow, 1).Value = s.CashierUsername;
                worksheet.Cell(currentRow, 2).Value = s.OpenedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(currentRow, 3).Value = s.ClosedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—";
                worksheet.Cell(currentRow, 4).Value = s.OpeningCash;
                worksheet.Cell(currentRow, 5).Value = s.HasClosingSummary ? s.SalesTotal : 0m;
                worksheet.Cell(currentRow, 6).Value = s.HasClosingSummary ? s.ExpectedCash : 0m;
                worksheet.Cell(currentRow, 7).Value = s.ClosingCash ?? 0m;
                worksheet.Cell(currentRow, 8).Value = diff;

                worksheet.Range(currentRow, 4, currentRow, 8).Style.NumberFormat.Format = CurrencyFormat;

                if (diff == 0)
                {
                    worksheet.Cell(currentRow, 8).Style.Font.FontColor = XLColor.DarkGreen;
                }
                else if (diff > 0)
                {
                    worksheet.Cell(currentRow, 8).Style.Font.FontColor = XLColor.FromHtml("#1565C0");
                    worksheet.Cell(currentRow, 8).Style.Font.Bold = true;
                }
                else
                {
                    worksheet.Cell(currentRow, 8).Style.Font.FontColor = XLColor.DarkRed;
                    worksheet.Cell(currentRow, 8).Style.Font.Bold = true;
                }

                currentRow++;
            }
        }

        FormatExcelTable(worksheet, 5, currentRow - 1, 8, freezeRows: 5);
        worksheet.Column(1).Width = 18;
        worksheet.Column(2).Width = 22;
        worksheet.Column(3).Width = 22;
        worksheet.Column(4).Width = 18;
        worksheet.Column(5).Width = 18;
        worksheet.Column(6).Width = 18;
        worksheet.Column(7).Width = 18;
        worksheet.Column(8).Width = 18;
    }

    private static void AddExcelOpenShifts(IXLWorksheet worksheet, IReadOnlyList<ShiftSummary> openShifts)
    {
        AddExcelSectionTitle(worksheet, 4, 1, 4, "Turnos Abiertos (Activos)");
        AddExcelHeader(worksheet, 5, "Cajero", "Fecha de Apertura", "Base de Apertura", "Estado");

        int currentRow = 6;
        if (openShifts.Count == 0)
        {
            worksheet.Range(currentRow, 1, currentRow, 4).Merge();
            worksheet.Cell(currentRow, 1).Value = "No hay turnos abiertos en este momento.";
            worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            currentRow++;
        }
        else
        {
            foreach (var s in openShifts)
            {
                worksheet.Cell(currentRow, 1).Value = s.CashierUsername;
                worksheet.Cell(currentRow, 2).Value = s.OpenedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(currentRow, 3).Value = s.OpeningCash;
                worksheet.Cell(currentRow, 4).Value = "Abierto (Activo)";

                worksheet.Cell(currentRow, 3).Style.NumberFormat.Format = CurrencyFormat;
                worksheet.Cell(currentRow, 4).Style.Font.FontColor = XLColor.DarkGreen;
                worksheet.Cell(currentRow, 4).Style.Font.Bold = true;

                currentRow++;
            }
        }

        FormatExcelTable(worksheet, 5, currentRow - 1, 4, freezeRows: 5);
        worksheet.Column(1).Width = 18;
        worksheet.Column(2).Width = 22;
        worksheet.Column(3).Width = 18;
        worksheet.Column(4).Width = 18;
    }
}
