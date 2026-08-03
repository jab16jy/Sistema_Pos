using System.Globalization;
using POS.Domain.Enums;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Shared.Cashier;

/// <summary>One leg of a split payment — a fixed amount tendered with a single method.</summary>
public sealed class PaymentTenderViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    public PaymentTenderViewModel(PaymentMethod method, string label, decimal amount, string? bankOrPlatform = null)
    {
        Method = method;
        Label = label;
        Amount = amount;
        BankOrPlatform = bankOrPlatform;
    }

    public PaymentMethod Method { get; }
    public string Label { get; }
    public decimal Amount { get; }
    public string? BankOrPlatform { get; }

    public bool IsTransfer => Method == PaymentMethod.Transfer;

    public string AmountText => $"${Amount.ToString("N0", ColombianCulture)}";

    public string DisplayText => IsTransfer && !string.IsNullOrWhiteSpace(BankOrPlatform)
        ? $"{Label} ({BankOrPlatform})"
        : Label;
}
