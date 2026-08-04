namespace POS.Domain.Enums;

/// <summary>
/// Single source for how a tendered payment reads on receipts and invoices, so the printed
/// ticket and the PDF never disagree about how a sale was paid. The platform belongs in the
/// label because "Transferencia" on its own does not say which account received the money.
/// </summary>
public static class PaymentMethodLabel
{
    public static string For(PaymentMethod method, string? bankOrPlatform = null)
    {
        string name = method switch
        {
            PaymentMethod.Cash => "Efectivo",
            PaymentMethod.DebitCard => "Tarjeta débito",
            PaymentMethod.CreditCard => "Tarjeta crédito",
            PaymentMethod.Transfer => "Transferencia",
            PaymentMethod.LoyaltyPoints => "Puntos de lealtad",
            _ => method.ToString()
        };

        return string.IsNullOrWhiteSpace(bankOrPlatform) ? name : $"{name} · {bankOrPlatform}";
    }
}
