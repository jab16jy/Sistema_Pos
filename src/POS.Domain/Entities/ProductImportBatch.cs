namespace POS.Domain.Entities;

public sealed class ProductImportBatch
{
    private ProductImportBatch()
    {
        SourceFileName = string.Empty;
    }

    public ProductImportBatch(string sourceFileName, int totalRows)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
            throw new ArgumentException("Source file name is required.", nameof(sourceFileName));

        if (totalRows < 0)
            throw new ArgumentOutOfRangeException(nameof(totalRows), "Total rows cannot be negative.");

        Id = Guid.NewGuid();
        SourceFileName = sourceFileName.Trim();
        TotalRows = totalRows;
        ImportedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string SourceFileName { get; private set; }
    public int TotalRows { get; private set; }
    public int ImportedRows { get; private set; }
    public int SkippedRows { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }

    public void RegisterImportedRow() => ImportedRows++;

    public void RegisterSkippedRow() => SkippedRows++;
}
