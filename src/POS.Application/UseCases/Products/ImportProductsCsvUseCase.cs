using System.Globalization;
using System.Text;
using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Products;

public sealed class ImportProductsCsvUseCase
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryRepository? _inventory;
    private readonly ICategoryRepository? _categories;

    // Maps both the internal schema (name,categoryId,costPrice,...) and external accounting-software
    // exports (Alegra/Siigo style: Tipo,Código,Nombre,Unidad,Precios,Impuestos,Stock,Estado) to the
    // same canonical field set, so one parser handles either file without a format toggle.
    private static readonly IReadOnlyDictionary<string, string> HeaderAliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["barcode"] = "barcode",
        ["codigo"] = "barcode",
        ["name"] = "name",
        ["nombre"] = "name",
        ["categoryid"] = "categoryid",
        ["tipo"] = "categoryname",
        ["categoria"] = "categoryname",
        ["categoryname"] = "categoryname",
        ["costprice"] = "costprice",
        ["preciocosto"] = "costprice",
        ["saleprice"] = "saleprice",
        ["precios"] = "saleprice",
        ["precio"] = "saleprice",
        ["currentstock"] = "currentstock",
        ["stock"] = "currentstock",
        ["minimumstock"] = "minimumstock",
        ["stockminimo"] = "minimumstock",
        ["description"] = "description",
        ["descripcion"] = "description",
        ["taxrate"] = "taxrate",
        ["impuestos"] = "taxrate",
        ["impuesto"] = "taxrate",
        ["estado"] = "status",
        ["status"] = "status",
    };

    public ImportProductsCsvUseCase(IProductRepository products, IUnitOfWork unitOfWork, IInventoryRepository? inventory = null, ICategoryRepository? categories = null)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _inventory = inventory;
        _categories = categories;
    }

    public async Task<ImportProductsCsvResult> ExecuteAsync(ImportProductsCsvRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CsvContent))
        {
            return new ImportProductsCsvResult(0, 0, ["El archivo CSV está vacío."]);
        }

        var lines = request.CsvContent
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseCsvLine)
            .ToList();

        if (lines.Count < 2)
        {
            return new ImportProductsCsvResult(0, 0, ["El CSV debe incluir encabezados y al menos un producto."]);
        }

        // Some exports prepend title/owner/NIT rows before the real header row; scan for it instead
        // of assuming line 0.
        int headerRowIndex = FindHeaderRowIndex(lines);
        var headers = lines[headerRowIndex]
            .Select((name, index) => new { Name = MapHeader(name), Index = index })
            .Where(h => !string.IsNullOrWhiteSpace(h.Name))
            .ToDictionary(h => h.Name, h => h.Index, StringComparer.Ordinal);

        var errors = new List<string>();
        var importedBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var categoryIdsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        var skipped = 0;

        for (var rowIndex = headerRowIndex + 1; rowIndex < lines.Count; rowIndex++)
        {
            var row = lines[rowIndex];
            var lineNo = rowIndex + 1;

            try
            {
                // Validate every field first so a malformed row never reserves a barcode
                // that a later valid row with the same code would need.
                string? rawBarcode = Get(row, headers, "barcode");
                string name = Required(row, headers, "name", lineNo);
                Guid categoryId = await ResolveCategoryAsync(row, headers, lineNo, categoryIdsByName, ct).ConfigureAwait(false);
                string? rawCost = Get(row, headers, "costprice");
                Money? cost = string.IsNullOrWhiteSpace(rawCost) ? null : new Money(ParseDecimal(rawCost, lineNo, "costPrice"));
                Money sale = new(ParseDecimal(Required(row, headers, "saleprice", lineNo), lineNo, "salePrice"));
                // Source systems sometimes use negative stock as an "untracked" sentinel rather than
                // a real negative balance; clamp to zero instead of letting it blow up StockQuantity.
                decimal initialStock = Math.Max(0m, ParseDecimal(Get(row, headers, "currentstock") ?? "0", lineNo, "currentStock"));
                StockQuantity minStock = new(ParseDecimal(Get(row, headers, "minimumstock") ?? "0", lineNo, "minimumStock"));
                string? description = Get(row, headers, "description");
                Barcode? barcodeVo = string.IsNullOrWhiteSpace(rawBarcode) ? null : new Barcode(rawBarcode);
                decimal taxRate = ParseTaxRate(Get(row, headers, "taxrate") ?? "19", lineNo);
                string? status = Get(row, headers, "status");

                // Only reserve the canonical barcode after the row is fully validated.
                string? canonical = barcodeVo?.Value;
                if (canonical is not null)
                {
                    if (!importedBarcodes.Add(canonical))
                    {
                        errors.Add($"Línea {lineNo}: el código de barras '{canonical}' está duplicado en el archivo CSV.");
                        skipped++;
                        continue;
                    }

                    if (await _products.GetByBarcodeAsync(canonical, ct).ConfigureAwait(false) is not null)
                    {
                        skipped++;
                        continue;
                    }
                }

                // Imported products start at zero stock; any initial balance is recorded as an
                // audited InitialBalance movement so saldo e historial reconcile from creation.
                // ponytail: the null ledger path exists only for unit fixtures; production DI injects it.
                bool ledgerAvailable = _inventory is not null;
                var seedStock = new StockQuantity(ledgerAvailable ? 0m : initialStock);

                var product = new Product(name, categoryId, cost, sale, seedStock, minStock, description, barcodeVo, taxRate);
                if (!string.IsNullOrWhiteSpace(status) && status.Contains("inactiv", StringComparison.OrdinalIgnoreCase))
                {
                    product.Deactivate();
                }

                await _products.AddAsync(product, ct).ConfigureAwait(false);

                if (ledgerAvailable && initialStock > 0m)
                {
                    InventoryMovement movement = product.RegisterStockMovement(
                        InventoryMovementType.InitialBalance, initialStock, "Saldo inicial (importación CSV)", request.OperatorUserId);
                    await _inventory!.AddMovementAsync(movement, ct).ConfigureAwait(false);
                }

                imported++;
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
            {
                errors.Add($"Línea {lineNo}: {ex.Message}");
                skipped++;
            }
        }

        if (imported > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return new ImportProductsCsvResult(imported, skipped, errors);
    }

    private async Task<Guid> ResolveCategoryAsync(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> headers,
        int lineNo,
        Dictionary<string, Guid> cache,
        CancellationToken ct)
    {
        string? rawId = Get(row, headers, "categoryid");
        if (!string.IsNullOrWhiteSpace(rawId))
        {
            return Guid.Parse(rawId);
        }

        string categoryName = Required(row, headers, "categoryname", lineNo);
        if (cache.TryGetValue(categoryName, out var cachedId))
        {
            return cachedId;
        }

        if (_categories is null)
        {
            throw new InvalidOperationException($"No se puede resolver la categoría '{categoryName}' sin acceso al repositorio de categorías.");
        }

        Category? existing = (await _categories.GetAllAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(c => string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase));

        Guid categoryId;
        if (existing is not null)
        {
            categoryId = existing.Id;
        }
        else
        {
            var category = new Category(categoryName);
            await _categories.AddAsync(category, ct).ConfigureAwait(false);
            // Flush immediately: the product row inserted right after this needs the category's FK
            // to already exist, and the batch-wide SaveChanges only happens once at the end.
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            categoryId = category.Id;
        }

        cache[categoryName] = categoryId;
        return categoryId;
    }

    private static int FindHeaderRowIndex(IReadOnlyList<IReadOnlyList<string>> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Select(MapHeader).Contains("name"))
            {
                return i;
            }
        }

        return 0;
    }

    private static string MapHeader(string header)
    {
        string normalized = NormalizeHeader(header);
        return HeaderAliases.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    private static string NormalizeHeader(string header)
    {
        string stripped = header.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        string decomposed = stripped.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? Get(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> headers, string name)
        => headers.TryGetValue(name, out var index) && index < row.Count ? row[index].Trim() : null;

    private static string Required(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> headers, string name, int lineNo)
        => string.IsNullOrWhiteSpace(Get(row, headers, name))
            ? throw new ArgumentException($"Falta la columna requerida '{name}' en la línea {lineNo}.")
            : Get(row, headers, name)!;

    private static decimal ParseDecimal(string value, int lineNo, string field)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("es-CO"), out parsed)
                ? parsed
                : throw new FormatException($"'{field}' no es un número válido en la línea {lineNo}.");

    private static decimal ParseTaxRate(string value, int lineNo)
    {
        // Accepts plain numbers ("19") and percentage labels ("IVA 19%").
        string digits = new(value.Where(c => char.IsAsciiDigit(c) || c is '.' or ',').ToArray());
        return string.IsNullOrWhiteSpace(digits) ? 0m : ParseDecimal(digits, lineNo, "taxRate");
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}
