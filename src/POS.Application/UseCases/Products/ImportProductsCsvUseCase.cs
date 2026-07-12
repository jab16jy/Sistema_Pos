using System.Globalization;
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

    public ImportProductsCsvUseCase(IProductRepository products, IUnitOfWork unitOfWork, IInventoryRepository? inventory = null)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _inventory = inventory;
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

        var headers = lines[0]
            .Select((name, index) => new { Name = NormalizeHeader(name), Index = index })
            .Where(h => !string.IsNullOrWhiteSpace(h.Name))
            .ToDictionary(h => h.Name, h => h.Index, StringComparer.OrdinalIgnoreCase);

        var errors = new List<string>();
        var importedBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var imported = 0;
        var skipped = 0;

        for (var rowIndex = 1; rowIndex < lines.Count; rowIndex++)
        {
            var row = lines[rowIndex];
            var lineNo = rowIndex + 1;

            try
            {
                // Validate every field first so a malformed row never reserves a barcode
                // that a later valid row with the same code would need.
                string? rawBarcode = Get(row, headers, "barcode");
                string name = Required(row, headers, "name", lineNo);
                Guid categoryId = Guid.Parse(Required(row, headers, "categoryid", lineNo));
                Money cost = new(ParseDecimal(Required(row, headers, "costprice", lineNo), lineNo, "costPrice"));
                Money sale = new(ParseDecimal(Required(row, headers, "saleprice", lineNo), lineNo, "salePrice"));
                decimal initialStock = ParseDecimal(Get(row, headers, "currentstock") ?? "0", lineNo, "currentStock");
                StockQuantity minStock = new(ParseDecimal(Get(row, headers, "minimumstock") ?? "0", lineNo, "minimumStock"));
                string? description = Get(row, headers, "description");
                Barcode? barcodeVo = string.IsNullOrWhiteSpace(rawBarcode) ? null : new Barcode(rawBarcode);
                decimal taxRate = ParseDecimal(Get(row, headers, "taxrate") ?? "19", lineNo, "taxRate");

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

    private static string NormalizeHeader(string header)
        => header.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

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

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
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
