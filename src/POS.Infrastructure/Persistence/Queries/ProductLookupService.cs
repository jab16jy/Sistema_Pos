using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Products;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class ProductLookupService : IProductLookupService
{
    private readonly AppDbContext _dbContext;

    public ProductLookupService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<ProductLookupResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        string normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0)
        {
            return [];
        }

        decimal? priceQuery = decimal.TryParse(normalizedQuery, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedPrice)
            ? parsedPrice
            : null;

        Dictionary<Guid, string> categoryNames = await _dbContext.Categories
            .AsNoTracking()
            .ToDictionaryAsync(category => category.Id, category => Normalize(category.Name), cancellationToken)
            .ConfigureAwait(false);

        // ponytail: in-memory scan of the active catalog per debounced keystroke keeps matching
        // accent-insensitive without schema changes; add a persisted normalized column if the
        // catalog grows past ~50k products.
        var candidates = await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.SourceCode,
                product.Barcode,
                product.CategoryId,
                SalePrice = product.SalePrice.Amount,
                product.CurrentStock,
                product.MinimumStock,
                product.UnitsPerBox,
                BoxPrice = product.BoxPrice.HasValue ? product.BoxPrice.Value.Amount : (decimal?)null
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates
            .Where(product =>
                Normalize(product.Name).Contains(normalizedQuery, StringComparison.Ordinal) ||
                Normalize(product.SourceCode).Contains(normalizedQuery, StringComparison.Ordinal) ||
                (product.Barcode is not null && Normalize(product.Barcode).Contains(normalizedQuery, StringComparison.Ordinal)) ||
                (categoryNames.TryGetValue(product.CategoryId, out string? categoryName) && categoryName.Contains(normalizedQuery, StringComparison.Ordinal)) ||
                (priceQuery is not null && product.SalePrice == priceQuery.Value))
            .OrderBy(product => product.Name, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .Select(product => new ProductLookupResult(
                product.Id,
                product.Name,
                product.Barcode ?? product.SourceCode,
                product.SalePrice,
                product.CurrentStock,
                product.MinimumStock,
                product.UnitsPerBox,
                product.BoxPrice))
            .ToList();
    }

    public async Task<ProductLookupResult?> FindByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        string exactBarcode = barcode.Trim();
        return await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.Barcode == exactBarcode)
            .Select(product => new ProductLookupResult(
                product.Id,
                product.Name,
                product.Barcode!,
                product.SalePrice.Amount,
                product.CurrentStock,
                product.MinimumStock,
                product.UnitsPerBox,
                product.BoxPrice.HasValue ? product.BoxPrice.Value.Amount : null))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Lowercases and strips accents so "azucar" matches "Azúcar".</summary>
    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
