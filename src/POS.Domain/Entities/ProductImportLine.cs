namespace POS.Domain.Entities;

public sealed class ProductImportLine
{
    private ProductImportLine()
    {
        SourceType = string.Empty;
        SourceCode = string.Empty;
        SourceName = string.Empty;
        SourcePrice = string.Empty;
        SourceTax = string.Empty;
        SourceStock = string.Empty;
        Status = string.Empty;
    }

    public ProductImportLine(
        Guid batchId,
        int rowNumber,
        string sourceType,
        string sourceCode,
        string sourceName,
        string? sourceUnit,
        string sourcePrice,
        string sourceTax,
        string sourceStock,
        decimal normalizedStock,
        string status,
        string? notes,
        Guid? importedProductId = null)
    {
        if (rowNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowNumber), "Row number must be greater than zero.");

        if (string.IsNullOrWhiteSpace(sourceType))
            throw new ArgumentException("Source type is required.", nameof(sourceType));

        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code is required.", nameof(sourceCode));

        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source name is required.", nameof(sourceName));

        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status is required.", nameof(status));

        Id = Guid.NewGuid();
        BatchId = batchId;
        RowNumber = rowNumber;
        SourceType = sourceType.Trim();
        SourceCode = sourceCode.Trim();
        SourceName = sourceName.Trim();
        SourceUnit = string.IsNullOrWhiteSpace(sourceUnit) ? null : sourceUnit.Trim();
        SourcePrice = sourcePrice.Trim();
        SourceTax = sourceTax.Trim();
        SourceStock = sourceStock.Trim();
        NormalizedStock = normalizedStock;
        Status = status.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ImportedProductId = importedProductId;
    }

    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public int RowNumber { get; private set; }
    public string SourceType { get; private set; }
    public string SourceCode { get; private set; }
    public string SourceName { get; private set; }
    public string? SourceUnit { get; private set; }
    public string SourcePrice { get; private set; }
    public string SourceTax { get; private set; }
    public string SourceStock { get; private set; }
    public decimal NormalizedStock { get; private set; }
    public string Status { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ImportedProductId { get; private set; }
}
