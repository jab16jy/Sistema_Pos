using POS.Domain.Enums;

namespace POS.Presentation.Shared.Cashier;

public sealed record PaymentMethodOption(PaymentMethod Method, string Label);
