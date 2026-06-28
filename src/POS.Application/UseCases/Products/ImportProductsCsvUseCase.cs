using System.Globalization;
using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Products;

public sealed class ImportProductsCsvUseCase
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public ImportProductsCsvUseCase(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products = products;
        _unitOfWork = unitOfWork;
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
        var imported = 0;
        var skipped = 0;

        for (var rowIndex = 1; rowIndex < lines.Count; rowIndex++)
        {
            var row = lines[rowIndex];
            var lineNo = rowIndex + 1;

            try
            {
                var barcode = Get(row, headers, "barcode");
                if (!string.IsNullOrWhiteSpace(barcode) &&
                    await _products.GetByBarcodeAsync(barcode, ct).ConfigureAwait(false) is not null)
                {
                    skipped++;
                    continue;
                }

                var product = new Product(
                    Required(row, headers, "name", lineNo),
                    Guid.Parse(Required(row, headers, "categoryid", lineNo)),
                    new Money(ParseDecimal(Required(row, headers, "costprice", lineNo), lineNo, "costPrice")),
                    new Money(ParseDecimal(Required(row, headers, "saleprice", lineNo), lineNo, "salePrice")),
                    new StockQuantity(ParseDecimal(Get(row, headers, "currentstock") ?? "0", lineNo, "currentStock")),
                    new StockQuantity(ParseDecimal(Get(row, headers, "minimumstock") ?? "0", lineNo, "minimumStock")),
                    Get(row, headers, "description"),
                    string.IsNullOrWhiteSpace(barcode) ? null : new Barcode(barcode),
                    ParseDecimal(Get(row, headers, "taxrate") ?? "19", lineNo, "taxRate"));

                await _products.AddAsync(product, ct).ConfigureAwait(false);
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
