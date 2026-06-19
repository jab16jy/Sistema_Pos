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

        var result = useCase.Execute(new PaymentPreviewRequest(7500m, PaymentMethod.Transfer, 0m));

        Assert.True(result.CanConfirm);
        Assert.Equal(0m, result.ChangeAmount);
        Assert.Contains("digital", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
