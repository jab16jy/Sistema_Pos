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
        Assert.Contains("platform", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_NonTransferWithPlatform_DisablesConfirmation()
    {
        var result = new PreviewPaymentUseCase().Execute(
            new PaymentPreviewRequest(7_500m, PaymentMethod.Cash, 7_500m, BankOrPlatform: "Nequi"));

        Assert.False(result.CanConfirm);
        Assert.Contains("only valid", result.Message, StringComparison.OrdinalIgnoreCase);
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
}
