using System.Linq;
using POS.Application.DTOs.Sales;
using POS.Application.Common;
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

        var deferred = request.DeferredAmount > 0m ? new Money(request.DeferredAmount) : Money.Zero;

        // Split-payment path: several tenders, possibly leaving a remainder that becomes deferred.
        if (request.Tenders is { Count: > 0 } tenders && deferred.Amount == 0m)
        {
            foreach (PaymentTender tender in tenders)
            {
                try
                {
                    PaymentPlatforms.Validate(tender.Method, tender.BankOrPlatform);
                }
                catch (InvalidOperationException exception)
                {
                    return new PaymentPreviewResult(false, 0m, exception.Message);
                }
            }

            decimal nonCashCovered = tenders.Where(t => t.Method != PaymentMethod.Cash).Sum(t => t.Amount);
            decimal cashReceived = tenders.Where(t => t.Method == PaymentMethod.Cash).Sum(t => t.Amount);

            // Invariant: payment legs may not over-cover the total. Non-cash legs are persisted
            // at their full amount (only the cash leg is capped for change), so an over-covering
            // digital tender would make Sale.Complete()'s exact TotalPaid == Total check fail.
            if (nonCashCovered > total.Amount)
            {
                decimal excess = nonCashCovered - total.Amount;
                return new PaymentPreviewResult(false, 0m,
                    $"Los pagos digitales superan el total por ${excess:N0}. Reduce los pagos para que no superen el total.");
            }

            decimal covered = nonCashCovered + cashReceived;

            if (covered < total.Amount)
            {
                // The remainder becomes a deferred balance rather than blocking confirmation — the
                // caller (CashierViewModel) still gates this on requiring an identified customer,
                // and CreateSaleUseCase re-validates the maximum deferred percent server-side.
                decimal remainder = total.Amount - covered;
                return new PaymentPreviewResult(true, 0m,
                    $"Venta con crédito: ${covered:N0} cobrado en pagos combinados, ${remainder:N0} queda como saldo pendiente.",
                    DeferredBalance: remainder);
            }

            decimal splitChange = Math.Max(0m, cashReceived - (total.Amount - nonCashCovered));
            return new PaymentPreviewResult(true, splitChange, "Pagos combinados. Verifica el detalle antes de confirmar.");
        }

        // Settlement plan path: collected + deferred must cover total
        if (deferred.Amount > 0m)
        {
            var collected = request.ReceivedAmount;
            if (collected > 0m)
            {
                try
                {
                    PaymentPlatforms.Validate(request.PaymentMethod, request.BankOrPlatform);
                }
                catch (InvalidOperationException exception)
                {
                    return new PaymentPreviewResult(false, 0m, exception.Message);
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.BankOrPlatform))
            {
                return new PaymentPreviewResult(false, 0m,
                    "La plataforma de pago requiere un monto cobrado.");
            }

            if (collected + deferred.Amount != total.Amount)
            {
                return new PaymentPreviewResult(false, 0m,
                    "El monto cobrado más el crédito debe ser exactamente igual al total de la venta.");
            }

            return new PaymentPreviewResult(true, 0m,
                $"Venta con crédito: ${collected:N0} cobrado, ${deferred.Amount:N0} queda como saldo pendiente.",
                DeferredBalance: deferred.Amount);
        }

        try
        {
            PaymentPlatforms.Validate(request.PaymentMethod, request.BankOrPlatform);
        }
        catch (InvalidOperationException exception)
        {
            return new PaymentPreviewResult(false, 0m, exception.Message);
        }

        // Full-payment path (no deferred)
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
