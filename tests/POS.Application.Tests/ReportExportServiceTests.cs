using ClosedXML.Excel;
using POS.Application.DTOs.Reporting;
using POS.Infrastructure.Services;
using Xunit;

namespace POS.Application.Tests;

public sealed class ReportExportServiceTests
{
    private readonly ReportExportService _service = new();

    [Fact]
    public async Task ExportOperationalReportPdfAsync_GeneratesAPdfWithReportContent()
    {
        byte[] document = await _service.ExportOperationalReportPdfAsync(
            OperationalReport(),
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 1, 23, 59, 59, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(document);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(document, 0, 4));
    }

    [Fact]
    public async Task ExportOperationalReportExcelAsync_CreatesSummaryAndCashReconciliation()
    {
        byte[] document = await _service.ExportOperationalReportExcelAsync(
            OperationalReport(),
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 1, 23, 59, 59, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        using var stream = new MemoryStream(document);
        using var workbook = new XLWorkbook(stream);

        IXLWorksheet summary = workbook.Worksheet("Resumen");
        Assert.NotNull(workbook.Worksheet("Conciliación de caja"));
        Assert.Equal("REPORTE OPERACIONAL", summary.Cell("A1").GetString());
        Assert.Equal("Total facturado", summary.Cell("A5").GetString());
        Assert.Equal(1_500_000m, summary.Cell("A6").GetValue<decimal>());
        Assert.Equal("Efectivo esperado", summary.Cell("B16").GetString());
    }

    [Fact]
    public async Task ExportProfitReportExcelAsync_CreatesDashboardAndDetailedSheets()
    {
        byte[] document = await _service.ExportProfitReportExcelAsync(
            [new TopSellerResult(Guid.NewGuid(), "Crema hidratante", 12, 360_000m)],
            new PeriodComparisonResult(360_000m, 300_000m, 120_000m, 110_000m, 240_000m, 190_000m, 20m),
            [new ProductMarginResult(Guid.NewGuid(), "Crema hidratante", 360_000m, 120_000m, 240_000m, 66.67m)],
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 7),
            TestContext.Current.CancellationToken);

        using var stream = new MemoryStream(document);
        using var workbook = new XLWorkbook(stream);

        Assert.Equal("REPORTE DE RENTABILIDAD", workbook.Worksheet("Resumen").Cell("A1").GetString());
        Assert.Equal("PRODUCTOS MÁS VENDIDOS", workbook.Worksheet("Top vendidos").Cell("A1").GetString());
        Assert.Equal("MARGEN POR PRODUCTO", workbook.Worksheet("Márgenes").Cell("A1").GetString());
        Assert.Equal("Crema hidratante", workbook.Worksheet("Top vendidos").Cell("B5").GetString());
        Assert.Equal("Rentable", workbook.Worksheet("Márgenes").Cell("F5").GetString());
    }

    [Fact]
    public async Task ExportProfitReportPdfAsync_ExplainsAnEmptyPeriod()
    {
        byte[] document = await _service.ExportProfitReportPdfAsync(
            [],
            new PeriodComparisonResult(0m, 0m, 0m, 0m, 0m, 0m, 0m),
            [],
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 7),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(document);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(document, 0, 4));
    }

    private static OperationalReportDto OperationalReport() => new(
        BilledSalesTotal: 1_500_000m,
        SaleCashCollected: 900_000m,
        SaleDigitalCollected: 600_000m,
        ReceivableSettlementCash: 100_000m,
        ReceivableSettlementDigital: 50_000m,
        ReceivableSettlementTotal: 150_000m,
        ExpectedDrawerCash: 1_000_000m);
}
