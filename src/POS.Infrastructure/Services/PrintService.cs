using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class PrintService : IPrintService
{
    public Task PrintReceiptAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        // ESC/POS implementation belongs here. Keep WPF and Application free of printer details.
        throw new NotImplementedException("ESC/POS receipt printing is not implemented yet.");
    }
}
