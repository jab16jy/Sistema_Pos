namespace POS.Application.Interfaces;

public interface IPrintService
{
    Task PrintReceiptAsync(Guid saleId, CancellationToken cancellationToken = default);
}
