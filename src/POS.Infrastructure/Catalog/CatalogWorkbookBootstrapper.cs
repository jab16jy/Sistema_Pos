using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Catalog;

internal static class CatalogWorkbookBootstrapper
{
    private const string CatalogWorkbookFileName = "Gestión de productos y servicios-20260211103730.xlsx";
    private static readonly string[] HeaderRow = ["Tipo", "Código", "Nombre", "Unidad", "Precios", "Impuestos", "Stock", "Estado"];

    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        string? workbookPath = ResolveWorkbookPath();
        if (string.IsNullOrWhiteSpace(workbookPath) || !File.Exists(workbookPath))
        {
            return;
        }

        string sourceFileName = Path.GetFileName(workbookPath);
        bool alreadyImported = await dbContext.ProductImportBatches
            .AsNoTracking()
            .AnyAsync(batch => batch.SourceFileName == sourceFileName, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyImported)
        {
            return;
        }

        CatalogSeedReferences references = await EnsureReferenceDataAsync(dbContext, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<CatalogWorkbookRow> rows = ReadRows(workbookPath);
        var batch = new ProductImportBatch(sourceFileName, rows.Count);
        dbContext.ProductImportBatches.Add(batch);

        foreach (CatalogWorkbookRow row in rows)
        {
            Product product = CreateProduct(row, references);
            dbContext.Products.Add(product);

            dbContext.ProductImportLines.Add(new ProductImportLine(
                batch.Id,
                row.RowNumber,
                row.Type,
                row.Code,
                row.Name,
                row.Unit,
                row.PriceRaw,
                row.TaxRaw,
                row.StockRaw,
                row.NormalizedStock,
                "Imported",
                row.Notes,
                product.Id));

            batch.RegisterImportedRow();
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CatalogSeedReferences> EnsureReferenceDataAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        TaxProfile iva19 = await GetOrCreateTaxProfileAsync(dbContext, "IVA_19", "IVA 19%", 19m, cancellationToken).ConfigureAwait(false);
        TaxProfile iva0 = await GetOrCreateTaxProfileAsync(dbContext, "IVA_0", "IVA 0%", 0m, cancellationToken).ConfigureAwait(false);
        TaxProfile noTax = await GetOrCreateTaxProfileAsync(dbContext, "NO_TAX", "Sin impuesto", 0m, cancellationToken).ConfigureAwait(false);
        UnitOfMeasure unit = await GetOrCreateUnitAsync(dbContext, "unidad", "und", cancellationToken).ConfigureAwait(false);
        Category category = await GetOrCreateCategoryAsync(dbContext, "Sin categoría", cancellationToken).ConfigureAwait(false);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CatalogSeedReferences(category.Id, unit.Id, iva19.Id, iva0.Id, noTax.Id);
    }

    private static async Task<TaxProfile> GetOrCreateTaxProfileAsync(AppDbContext dbContext, string code, string name, decimal rate, CancellationToken cancellationToken)
    {
        TaxProfile? existing = await dbContext.TaxProfiles.FirstOrDefaultAsync(profile => profile.Code == code, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = new TaxProfile(code, name, rate);
        dbContext.TaxProfiles.Add(created);
        return created;
    }

    private static async Task<UnitOfMeasure> GetOrCreateUnitAsync(AppDbContext dbContext, string name, string abbreviation, CancellationToken cancellationToken)
    {
        UnitOfMeasure? existing = await dbContext.UnitOfMeasures.FirstOrDefaultAsync(unit => unit.Name == name, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = new UnitOfMeasure(name, abbreviation);
        dbContext.UnitOfMeasures.Add(created);
        return created;
    }

    private static async Task<Category> GetOrCreateCategoryAsync(AppDbContext dbContext, string name, CancellationToken cancellationToken)
    {
        Category? existing = await dbContext.Categories.FirstOrDefaultAsync(category => category.Name == name, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = new Category(name);
        dbContext.Categories.Add(created);
        return created;
    }

    private static Product CreateProduct(CatalogWorkbookRow row, CatalogSeedReferences references)
    {
        CatalogItemType itemType = row.IsService ? CatalogItemType.Service : CatalogItemType.Product;
        bool tracksInventory = !row.IsService;
        Guid taxProfileId = ResolveTaxProfileId(row.TaxRaw, references);
        decimal taxRate = ResolveTaxRate(row.TaxRaw);
        Barcode? barcode = TryCreateBarcode(row.Code, itemType);

        return Product.CreateImportedCatalogItem(
            row.Code,
            row.Name,
            references.DefaultCategoryId,
            new Money(row.Price),
            new StockQuantity(row.NormalizedStock),
            new StockQuantity(0m),
            itemType,
            tracksInventory,
            references.DefaultUnitOfMeasureId,
            taxProfileId,
            description: row.IsService ? "Servicio importado desde catálogo Excel." : null,
            barcode: barcode,
            costPrice: null,
            subCategoryId: null,
            taxRate: taxRate,
            isActive: row.IsActive);
    }

    private static Guid ResolveTaxProfileId(string taxRaw, CatalogSeedReferences references)
    {
        string normalized = NormalizeTax(taxRaw);
        return normalized switch
        {
            "IVA 19%" => references.Iva19Id,
            "IVA 0%" => references.Iva0Id,
            _ => references.NoTaxId
        };
    }

    private static decimal ResolveTaxRate(string taxRaw)
    {
        string normalized = NormalizeTax(taxRaw);
        return normalized switch
        {
            "IVA 19%" => 19m,
            "IVA 0%" => 0m,
            _ => 0m
        };
    }

    private static string NormalizeTax(string? taxRaw)
        => string.IsNullOrWhiteSpace(taxRaw) ? "NO_TAX" : taxRaw.Trim().ToUpperInvariant();

    private static Barcode? TryCreateBarcode(string sourceCode, CatalogItemType itemType)
    {
        if (itemType != CatalogItemType.Product)
        {
            return null;
        }

        try
        {
            return new Barcode(sourceCode);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? ResolveWorkbookPath()
    {
        string? envPath = Environment.GetEnvironmentVariable("POS_CATALOG_SOURCE_XLSX");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "docs md", "docs", CatalogWorkbookFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        string cwdCandidate = Path.Combine(Environment.CurrentDirectory, "docs md", "docs", CatalogWorkbookFileName);
        return File.Exists(cwdCandidate) ? cwdCandidate : null;
    }

    private static IReadOnlyList<CatalogWorkbookRow> ReadRows(string workbookPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(workbookPath);
        XNamespace spreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        Dictionary<int, string> sharedStrings = ReadSharedStrings(archive, spreadsheetNs);
        string worksheetPath = ResolveFirstWorksheetPath(archive, spreadsheetNs, relationshipNs);
        ZipArchiveEntry worksheetEntry = archive.GetEntry(worksheetPath)
            ?? throw new InvalidOperationException($"Worksheet '{worksheetPath}' was not found in the catalog workbook.");

        XDocument worksheet = XDocument.Load(worksheetEntry.Open());
        List<(int RowNumber, List<string> Cells)> rows = worksheet
            .Descendants(spreadsheetNs + "row")
            .Select((row, index) => (
                RowNumber: ParseRowNumber(row, index + 1),
                Cells: ReadCells(row, spreadsheetNs, sharedStrings)))
            .ToList();

        int headerIndex = rows.FindIndex(row => HasExpectedHeader(row.Cells));
        if (headerIndex < 0)
        {
            throw new InvalidOperationException("No se encontró el encabezado esperado en el Excel de catálogo.");
        }

        return rows
            .Skip(headerIndex + 1)
            .Where(row => row.Cells.Count >= HeaderRow.Length && row.Cells.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .Select(row => CatalogWorkbookRow.FromCells(row.RowNumber, row.Cells))
            .ToList();
    }

    private static Dictionary<int, string> ReadSharedStrings(ZipArchive archive, XNamespace spreadsheetNs)
    {
        ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        XDocument document = XDocument.Load(entry.Open());
        return document
            .Descendants(spreadsheetNs + "si")
            .Select((element, index) => new
            {
                index,
                value = string.Concat(element.Descendants(spreadsheetNs + "t").Select(text => text.Value))
            })
            .ToDictionary(item => item.index, item => item.value);
    }

    private static string ResolveFirstWorksheetPath(ZipArchive archive, XNamespace spreadsheetNs, XNamespace relationshipNs)
    {
        XDocument workbook = XDocument.Load((archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidOperationException("The workbook definition was not found.")).Open());
        XDocument relationships = XDocument.Load((archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidOperationException("The workbook relationships were not found.")).Open());

        string relationshipId = workbook
            .Descendants(spreadsheetNs + "sheet")
            .Select(sheet => sheet.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))?.Value)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))
            ?? throw new InvalidOperationException("The workbook does not contain any worksheets.");

        string? target = relationships
            .Descendants(relationshipNs + "Relationship")
            .Where(relationship => string.Equals((string?)relationship.Attribute("Id"), relationshipId, StringComparison.Ordinal))
            .Select(relationship => (string?)relationship.Attribute("Target"))
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("The workbook worksheet target could not be resolved.");
        }

        string normalized = target.Replace('\\', '/');
        normalized = normalized.StartsWith("/") ? normalized.TrimStart('/') : $"xl/{normalized.TrimStart('/')}";
        return normalized;
    }

    private static int ParseRowNumber(XElement row, int fallbackRowNumber)
        => int.TryParse((string?)row.Attribute("r"), out int parsed) && parsed > 0 ? parsed : fallbackRowNumber;

    private static List<string> ReadCells(XElement row, XNamespace spreadsheetNs, IReadOnlyDictionary<int, string> sharedStrings)
    {
        var values = new List<string>();
        int currentColumn = 0;

        foreach (XElement cell in row.Elements(spreadsheetNs + "c"))
        {
            string? reference = (string?)cell.Attribute("r");
            if (!string.IsNullOrWhiteSpace(reference))
            {
                int targetColumn = GetColumnIndex(reference);
                while (currentColumn < targetColumn)
                {
                    values.Add(string.Empty);
                    currentColumn++;
                }
            }

            values.Add(ReadCellValue(cell, spreadsheetNs, sharedStrings));
            currentColumn++;
        }

        return values;
    }

    private static int GetColumnIndex(string reference)
    {
        int column = 0;
        foreach (char ch in reference)
        {
            if (!char.IsLetter(ch))
            {
                break;
            }

            column = (column * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return Math.Max(0, column - 1);
    }

    private static string ReadCellValue(XElement cell, XNamespace spreadsheetNs, IReadOnlyDictionary<int, string> sharedStrings)
    {
        string? type = (string?)cell.Attribute("t");

        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(cell.Descendants(spreadsheetNs + "t").Select(text => text.Value));
        }

        string? raw = cell.Element(spreadsheetNs + "v")?.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Concat(cell.Descendants(spreadsheetNs + "t").Select(text => text.Value));
        }

        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase) && int.TryParse(raw, out int sharedIndex) && sharedStrings.TryGetValue(sharedIndex, out string? shared))
        {
            return shared;
        }

        return raw;
    }

    private static bool HasExpectedHeader(IReadOnlyList<string> cells)
    {
        if (cells.Count < HeaderRow.Length)
        {
            return false;
        }

        for (int index = 0; index < HeaderRow.Length; index++)
        {
            if (!string.Equals(cells[index]?.Trim(), HeaderRow[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static decimal ParseDecimal(string raw)
    {
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal invariant))
        {
            return invariant;
        }

        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("es-CO"), out decimal esCo))
        {
            return esCo;
        }

        throw new FormatException($"No se pudo convertir '{raw}' a decimal.");
    }

    private readonly record struct CatalogSeedReferences(
        Guid DefaultCategoryId,
        Guid DefaultUnitOfMeasureId,
        Guid Iva19Id,
        Guid Iva0Id,
        Guid NoTaxId);

    private sealed record CatalogWorkbookRow(
        int RowNumber,
        string Type,
        string Code,
        string Name,
        string? Unit,
        decimal Price,
        string PriceRaw,
        string TaxRaw,
        decimal RawStock,
        string StockRaw,
        decimal NormalizedStock,
        bool IsActive)
    {
        public bool IsService => string.Equals(Type, "Servicio", StringComparison.OrdinalIgnoreCase);

        public string? Notes => RawStock < 0m
            ? $"Stock original del Excel normalizado a 0. Valor fuente: {RawStock.ToString(CultureInfo.InvariantCulture)}."
            : null;

        public static CatalogWorkbookRow FromCells(int rowNumber, IReadOnlyList<string> cells)
        {
            string type = cells[0].Trim();
            string code = cells[1].Trim();
            string name = cells[2].Trim();
            string? unit = string.IsNullOrWhiteSpace(cells[3]) ? null : cells[3].Trim();
            string priceRaw = cells[4].Trim();
            string taxRaw = string.IsNullOrWhiteSpace(cells[5]) ? "NO_TAX" : cells[5].Trim();
            string stockRaw = cells[6].Trim();
            string state = cells[7].Trim();

            decimal price = ParseDecimal(priceRaw);
            decimal rawStock = ParseDecimal(stockRaw);
            bool isService = string.Equals(type, "Servicio", StringComparison.OrdinalIgnoreCase);
            decimal normalizedStock = isService ? 0m : Math.Max(0m, rawStock);
            bool isActive = string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase);

            return new CatalogWorkbookRow(rowNumber, type, code, name, unit, price, priceRaw, taxRaw, rawStock, stockRaw, normalizedStock, isActive);
        }
    }
}
