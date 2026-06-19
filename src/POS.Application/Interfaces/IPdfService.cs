namespace POS.Application.Interfaces;

public interface IPdfService
{
    Task<byte[]> GenerateQuotePdfAsync(Guid quoteId, CancellationToken ct = default);
    Task<byte[]> GenerateInvoicePdfAsync(Guid saleId, CancellationToken ct = default);
}
