using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Sales;
using POS.Domain.Enums;
using Xunit;

namespace POS.Application.Tests;

public sealed class PreviewPaymentUseCaseTests
{
    [Fact]
    public void Execute_CashPaymentWithEnoughReceived_CalculatesChange()
    {
        var useCase = new PreviewPaymentUseCase();

        var result = useCase.Execute(new PaymentPreviewRequest(7500m, PaymentMethod.Cash, 10000m));

        Assert.True(result.CanConfirm);
        Assert.Equal(2500m, result.ChangeAmount);
    }

    [Fact]
    public void Execute_CashPaymentBelowTotal_DisablesConfirmation()
    {
        var useCase = new PreviewPaymentUseCase();

        var result = useCase.Execute(new PaymentPreviewRequest(7500m, PaymentMethod.Cash, 5000m));

        Assert.False(result.CanConfirm);
        Assert.Equal(0m, result.ChangeAmount);
        Assert.Contains("no cubre", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_DigitalPayment_DoesNotIncreaseCashChange()
    {
        var useCase = new PreviewPaymentUseCase();

        var result = useCase.Execute(new PaymentPreviewRequest(7500m, PaymentMethod.Transfer, 0m, BankOrPlatform: "Nequi"));

        Assert.True(result.CanConfirm);
        Assert.Equal(0m, result.ChangeAmount);
        Assert.Contains("digital", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_TransferWithoutPlatform_DisablesConfirmation()
    {
        var result = new PreviewPaymentUseCase().Execute(
            new PaymentPreviewRequest(7_500m, PaymentMethod.Transfer, 0m));

        Assert.False(result.CanConfirm);
        Assert.Contains("plataforma", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_NonTransferWithPlatform_DisablesConfirmation()
    {
        var result = new PreviewPaymentUseCase().Execute(
            new PaymentPreviewRequest(7_500m, PaymentMethod.Cash, 7_500m, BankOrPlatform: "Nequi"));

        Assert.False(result.CanConfirm);
        Assert.Contains("solo aplica a transferencias", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- RED: Settlement plan preview tests ---

    [Fact]
    public void Execute_WithDeferredBalance_WhenCollectedPlusDeferredCoversTotal_CanConfirm()
    {
        var useCase = new PreviewPaymentUseCase();

        // Total 100000, collected 60000 cash, deferred 40000
        var result = useCase.Execute(new PaymentPreviewRequest(100_000m, PaymentMethod.Cash, 60_000m, 40_000m));

        Assert.True(result.CanConfirm);
        Assert.Equal(0m, result.ChangeAmount); // no change when deferred covers remaining
        Assert.Equal(40_000m, result.DeferredBalance);
        Assert.Contains("crédito", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithFullyDeferredBalance_DoesNotRequirePlatformWithoutCollectedPayment()
    {
        var result = new PreviewPaymentUseCase().Execute(
            new PaymentPreviewRequest(100_000m, PaymentMethod.Transfer, 0m, 100_000m));

        Assert.True(result.CanConfirm);
        Assert.Equal(100_000m, result.DeferredBalance);
    }

    [Fact]
    public void Execute_WithDeferredBalance_WhenCollectedPlusDeferredDoesNotCoverTotal_CannotConfirm()
    {
        var useCase = new PreviewPaymentUseCase();

        // Total 100000, collected 50000, deferred 10000 = only 60000 — not enough
        var result = useCase.Execute(new PaymentPreviewRequest(100_000m, PaymentMethod.Cash, 50_000m, 10_000m));

        Assert.False(result.CanConfirm);
        Assert.Equal(0m, result.DeferredBalance);
    }

    [Fact]
    public void Execute_WithZeroDeferredBalance_BehavesLikeNonDeferredPath()
    {
        var useCase = new PreviewPaymentUseCase();

        // Deferred 0 — same as original full-payment path
        var result = useCase.Execute(new PaymentPreviewRequest(7_500m, PaymentMethod.Cash, 10_000m, 0m));

        Assert.True(result.CanConfirm);
        Assert.Equal(2_500m, result.ChangeAmount);
        Assert.Equal(0m, result.DeferredBalance);
    }

    // --- Split-payment (multiple tenders) preview tests ---

    [Fact]
    public void Execute_WithTenders_CashPlusTransferCoveringTotal_CanConfirm()
    {
        var useCase = new PreviewPaymentUseCase();
        var tenders = new[]
        {
            new PaymentTender(PaymentMethod.Cash, 30_000m),
            new PaymentTender(PaymentMethod.Transfer, 70_000m, "Nequi")
        };

        var result = useCase.Execute(new PaymentPreviewRequest(100_000m, PaymentMethod.Cash, 0m, Tenders: tenders));

        Assert.True(result.CanConfirm);
        Assert.Equal(0m, result.ChangeAmount);
    }

    [Fact]
    public void Execute_WithTenders_TransferWithoutPlatform_DisablesConfirmation()
    {
        var useCase = new PreviewPaymentUseCase();
        var tenders = new[]
        {
            new PaymentTender(PaymentMethod.Cash, 30_000m),
            new PaymentTender(PaymentMethod.Transfer, 70_000m)
        };

        var result = useCase.Execute(new PaymentPreviewRequest(100_000m, PaymentMethod.Cash, 0m, Tenders: tenders));

        Assert.False(result.CanConfirm);
        Assert.Contains("plataforma", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithTenders_OverpaidInCash_CalculatesChangeOnCashLegOnly()
    {
        var useCase = new PreviewPaymentUseCase();
        var tenders = new[]
        {
            new PaymentTender(PaymentMethod.Transfer, 70_000m, "Nequi"),
            new PaymentTender(PaymentMethod.Cash, 40_000m)
        };

        // Total 100000, transfer covers 70000, cash tendered 40000 for the remaining 30000 -> 10000 change
        var result = useCase.Execute(new PaymentPreviewRequest(100_000m, PaymentMethod.Cash, 0m, Tenders: tenders));

        Assert.True(result.CanConfirm);
        Assert.Equal(10_000m, result.ChangeAmount);
    }

    [Fact]
    public void Execute_WithTenders_SumBelowTotal_SucceedsWithRemainderAsDeferredBalance()
    {
        var useCase = new PreviewPaymentUseCase();
        var tenders = new[]
        {
            new PaymentTender(PaymentMethod.Cash, 30_000m),
            new PaymentTender(PaymentMethod.Transfer, 50_000m, "Nequi")
        };

        // Total 100000, tenders sum to 80000 — the missing 20000 becomes a deferred balance instead
        // of blocking confirmation (root-cause fix: Tenders and DeferredAmount are no longer mutually
        // exclusive paths).
        var result = useCase.Execute(new PaymentPreviewRequest(100_000m, PaymentMethod.Cash, 0m, Tenders: tenders));

        Assert.True(result.CanConfirm);
        Assert.Equal(0m, result.ChangeAmount);
        Assert.Equal(20_000m, result.DeferredBalance);
        Assert.Contains("crédito", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithTenders_NonCashOverpay_DisablesConfirmation()
    {
        var useCase = new PreviewPaymentUseCase();
        var tenders = new[]
        {
            new PaymentTender(PaymentMethod.Transfer, 120_000m, "Nequi"),
            new PaymentTender(PaymentMethod.Cash, 10_000m)
        };

        // Total 100000 — the digital tender alone over-covers the total. Non-cash legs persist at
        // their full amount (only cash is capped), so confirming would make Sale.Complete() reject
        // the sale with TotalPaid != Total. The preview must refuse up front.
        var result = useCase.Execute(new PaymentPreviewRequest(100_000m, PaymentMethod.Cash, 0m, Tenders: tenders));

        Assert.False(result.CanConfirm);
        Assert.Equal(0m, result.ChangeAmount);
        Assert.Contains("superan el total", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithTenders_NonCashOverpay_StillRejectsEvenThoughOverallSumIsBelowTotal()
    {
        var useCase = new PreviewPaymentUseCase();
        var tenders = new[]
        {
            new PaymentTender(PaymentMethod.Transfer, 120_000m, "Nequi")
        };

        // Regression: the digital-overcoverage rejection must still fire before the new
        // remainder-becomes-deferred logic ever runs, even though the tenders' sum (120000)
        // technically covers the total (100000) — it's the wrong shape (non-cash alone over-covers).
        var result = useCase.Execute(new PaymentPreviewRequest(100_000m, PaymentMethod.Cash, 0m, Tenders: tenders));

        Assert.False(result.CanConfirm);
        Assert.Equal(0m, result.DeferredBalance);
        Assert.Contains("superan el total", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WithTenders_DigitalExactlyCoversTotal_CanConfirm()
    {
        var useCase = new PreviewPaymentUseCase();
        var tenders = new[]
        {
            new PaymentTender(PaymentMethod.Transfer, 100_000m, "Nequi")
        };

        var result = useCase.Execute(new PaymentPreviewRequest(100_000m, PaymentMethod.Cash, 0m, Tenders: tenders));

        Assert.True(result.CanConfirm);
        Assert.Equal(0m, result.ChangeAmount);
    }
}
