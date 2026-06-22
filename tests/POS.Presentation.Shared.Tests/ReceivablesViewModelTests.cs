using POS.Application.DTOs.Receivables;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class ReceivablesViewModelTests
{
    [Fact]
    public async Task LoadAsync_WhenReceivablesExist_PopulatesRows()
    {
        var customerId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var rows = new List<ReceivableSummary>
        {
            new ReceivableSummary(
                ReceivableId: Guid.NewGuid(),
                CustomerId: customerId,
                CustomerName: "Test Customer",
                SaleId: saleId,
                InvoiceNumber: "POS-20260101",
                OriginalBalance: 100_000m,
                OutstandingBalance: 60_000m,
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-2))
        };
        var queryService = new StubReceivableQueryService(rows);
        var vm = new ReceivablesViewModel(queryService);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Single(vm.Receivables);
        Assert.Equal("Test Customer", vm.Receivables[0].CustomerName);
        Assert.Equal(60_000m, vm.Receivables[0].OutstandingBalance);
    }

    [Fact]
    public async Task LoadAsync_WhenNoReceivables_HasEmptyCollection()
    {
        var queryService = new StubReceivableQueryService([]);
        var vm = new ReceivablesViewModel(queryService);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(vm.Receivables);
        Assert.Contains("sin", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyPaymentAsync_WithValidAmount_ReducesOutstandingBalance()
    {
        var receivableId = Guid.NewGuid();
        var rows = new List<ReceivableSummary>
        {
            new ReceivableSummary(receivableId, Guid.NewGuid(), "Cliente Test", Guid.NewGuid(),
                "POS-20260102", 50_000m, 50_000m, DateTimeOffset.UtcNow)
        };
        var queryService = new StubReceivableQueryService(rows);
        var applyPayment = new StubApplyReceivablePaymentUseCase();
        var vm = new ReceivablesViewModel(queryService, applyPayment);

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.SelectedReceivable = vm.Receivables[0];
        vm.PaymentAmount = 20_000m;

        await vm.ApplyPaymentAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, applyPayment.CallCount);
        Assert.Contains("abono", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyPaymentAsync_WhenAmountExceedsBalance_SetsErrorMessage()
    {
        var receivableId = Guid.NewGuid();
        var rows = new List<ReceivableSummary>
        {
            new ReceivableSummary(receivableId, Guid.NewGuid(), "Cliente Test", Guid.NewGuid(),
                "POS-20260103", 50_000m, 30_000m, DateTimeOffset.UtcNow)
        };
        var queryService = new StubReceivableQueryService(rows);
        var applyPayment = new StubApplyReceivablePaymentUseCase(throwOverpayment: true);
        var vm = new ReceivablesViewModel(queryService, applyPayment);

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.SelectedReceivable = vm.Receivables[0];
        vm.PaymentAmount = 50_000m; // more than outstanding

        await vm.ApplyPaymentAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, applyPayment.CallCount);
        Assert.Contains("excede", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    // --- Stubs ---

    private sealed class StubReceivableQueryService : IReceivableQueryService
    {
        private readonly IReadOnlyList<ReceivableSummary> _rows;
        public StubReceivableQueryService(IReadOnlyList<ReceivableSummary> rows) => _rows = rows;

        public Task<IReadOnlyList<ReceivableSummary>> GetOpenReceivablesAsync(
            Guid? customerId, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(_rows);
    }

    private sealed class StubApplyReceivablePaymentUseCase : IApplyReceivablePaymentUseCase
    {
        private readonly bool _throwOverpayment;
        public int CallCount { get; private set; }

        public StubApplyReceivablePaymentUseCase(bool throwOverpayment = false)
            => _throwOverpayment = throwOverpayment;

        public Task ExecuteAsync(Guid receivableId, decimal paymentAmount, CancellationToken ct = default)
        {
            if (_throwOverpayment)
            {
                throw new InvalidOperationException("Payment amount exceeds outstanding balance.");
            }

            CallCount++;
            return Task.CompletedTask;
        }
    }
}
