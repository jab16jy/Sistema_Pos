using POS.Application.DTOs.Sales;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Sales;

public sealed class PreviewPaymentUseCase
{
    public PaymentPreviewResult Execute(PaymentPreviewRequest request)
    {
        var total = new Money(request.TotalAmount);

        if (total.Amount == 0m)
        {
            return new PaymentPreviewResult(false, 0m, "Agrega productos antes de confirmar la venta.");
        }

        if (request.PaymentMethod != PaymentMethod.Cash)
        {
            return new PaymentPreviewResult(true, 0m, "Pago digital seleccionado. Verifica el comprobante antes de confirmar.");
        }

        var received = new Money(request.ReceivedAmount);
        if (received.Amount < total.Amount)
        {
            return new PaymentPreviewResult(false, 0m, "El efectivo recibido no cubre el total de la venta.");
        }

        var change = received - total;
        return new PaymentPreviewResult(true, change.Amount, "Cambio calculado. Puedes confirmar cuando el cliente reciba el cambio.");
    }
}
