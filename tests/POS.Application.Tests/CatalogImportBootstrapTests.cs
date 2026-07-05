using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Enums;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public sealed class CatalogImportBootstrapTests
{
    [Fact]
    public async Task DatabaseSchemaRolloutAsync_WhenWorkbookExists_ImportsCatalogAndNormalizesNegativeStock()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"pos-catalog-{Guid.NewGuid():N}.db");
        string workbookPath = Path.Combine(Path.GetTempPath(), $"catalog-{Guid.NewGuid():N}.xlsx");
        string? previousWorkbookPath = Environment.GetEnvironmentVariable("POS_CATALOG_SOURCE_XLSX");

        await File.WriteAllBytesAsync(dbPath, [], TestContext.Current.CancellationToken);
        CreateWorkbook(workbookPath);
        Environment.SetEnvironmentVariable("POS_CATALOG_SOURCE_XLSX", workbookPath);

        try
        {
            var services = new ServiceCollection();
            services.AddPosInfrastructure($"Data Source={dbPath}");
            await using ServiceProvider provider = services.BuildServiceProvider();

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var products = await dbContext.Products.OrderBy(product => product.SourceCode).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, products.Count);

            var product = Assert.Single(products, item => item.SourceCode == "PROD001");
            Assert.Equal(CatalogItemType.Product, product.ItemType);
            Assert.True(product.TracksInventory);
            Assert.Equal(0m, product.CurrentStock);
            Assert.Equal("PROD001", product.Barcode);
            Assert.Null(product.CostPrice);
            Assert.Equal(19m, product.TaxRate);

            var service = Assert.Single(products, item => item.SourceCode == "SERV001");
            Assert.Equal(CatalogItemType.Service, service.ItemType);
            Assert.False(service.TracksInventory);
            Assert.Equal(0m, service.CurrentStock);
            Assert.Null(service.Barcode);
            Assert.Null(service.CostPrice);
            Assert.Equal(0m, service.TaxRate);

            Assert.Equal(1, await dbContext.ProductImportBatches.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(2, await dbContext.ProductImportLines.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(3, await dbContext.TaxProfiles.CountAsync(TestContext.Current.CancellationToken));

            var normalizedLine = await dbContext.ProductImportLines
                .SingleAsync(line => line.SourceCode == "PROD001", TestContext.Current.CancellationToken);

            Assert.Equal(0m, normalizedLine.NormalizedStock);
            Assert.Contains("normalizado a 0", normalizedLine.Notes, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("POS_CATALOG_SOURCE_XLSX", previousWorkbookPath);
            SqliteConnection.ClearAllPools();

            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            if (File.Exists(workbookPath))
            {
                File.Delete(workbookPath);
            }
        }
    }

    private static void CreateWorkbook(string workbookPath)
    {
        using ZipArchive archive = ZipFile.Open(workbookPath, ZipArchiveMode.Create);

        WriteEntry(archive, "[Content_Types].xml", """
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
  <Default Extension="xml" ContentType="application/xml" />
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
</Types>
""");

        WriteEntry(archive, "_rels/.rels", """
<?xml version="1.0" encoding="utf-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
</Relationships>
""");

        WriteEntry(archive, "xl/workbook.xml", """
<?xml version="1.0" encoding="utf-8"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="Catalog" sheetId="1" r:id="rId1" />
  </sheets>
</workbook>
""");

        WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
<?xml version="1.0" encoding="utf-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
</Relationships>
""");

        WriteEntry(archive, "xl/worksheets/sheet1.xml", """
<?xml version="1.0" encoding="utf-8"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <sheetData>
    <row r="1">
      <c r="A1" t="inlineStr"><is><t>Tipo</t></is></c>
      <c r="B1" t="inlineStr"><is><t>Código</t></is></c>
      <c r="C1" t="inlineStr"><is><t>Nombre</t></is></c>
      <c r="D1" t="inlineStr"><is><t>Unidad</t></is></c>
      <c r="E1" t="inlineStr"><is><t>Precios</t></is></c>
      <c r="F1" t="inlineStr"><is><t>Impuestos</t></is></c>
      <c r="G1" t="inlineStr"><is><t>Stock</t></is></c>
      <c r="H1" t="inlineStr"><is><t>Estado</t></is></c>
    </row>
    <row r="2">
      <c r="A2" t="inlineStr"><is><t>Producto</t></is></c>
      <c r="B2" t="inlineStr"><is><t>PROD001</t></is></c>
      <c r="C2" t="inlineStr"><is><t>Serum</t></is></c>
      <c r="D2" t="inlineStr"><is><t>unidad</t></is></c>
      <c r="E2"><v>12000</v></c>
      <c r="F2" t="inlineStr"><is><t>IVA 19%</t></is></c>
      <c r="G2"><v>-5</v></c>
      <c r="H2" t="inlineStr"><is><t>Active</t></is></c>
    </row>
    <row r="3">
      <c r="A3" t="inlineStr"><is><t>Servicio</t></is></c>
      <c r="B3" t="inlineStr"><is><t>SERV001</t></is></c>
      <c r="C3" t="inlineStr"><is><t>Consulta</t></is></c>
      <c r="D3" t="inlineStr"><is><t></t></is></c>
      <c r="E3"><v>5000</v></c>
      <c r="F3" t="inlineStr"><is><t></t></is></c>
      <c r="G3"><v>0</v></c>
      <c r="H3" t="inlineStr"><is><t>Active</t></is></c>
    </row>
  </sheetData>
</worksheet>
""");
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
