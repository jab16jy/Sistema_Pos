using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Reporting;

/// <summary>
/// Closes the cash register for a period: computes separated totals from persisted records,
/// persists a CashClosing snapshot, and returns a closing summary with difference.
/// Rejects requests that do not provide counted cash.
/// </summary>
public sealed class CloseCashRegisterUseCase
{
    private readonly IOperationalReportQueryService _queryService;
    private readonly ICashClosingRepository _cashClosingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CloseCashRegisterUseCase(
        IOperationalReportQueryService queryService,
        ICashClosingRepository cashClosingRepository,
        IUnitOfWork unitOfWork)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _cashClosingRepository = cashClosingRepository ?? throw new ArgumentNullException(nameof(cashClosingRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CashClosingResult> ExecuteAsync(
        CashClosingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return new CashClosingResult(false, null, "Closing request cannot be null.");
        }

        if (request.CountedCash is null)
        {
            return new CashClosingResult(false, null,
                "Counted cash is required to close the register.");
        }

        if (request.CountedCash.Value < 0m)
        {
            return new CashClosingResult(false, null,
                "Counted cash cannot be negative.");
        }

        if (request.From >= request.To)
        {
            return new CashClosingResult(false, null,
                "Invalid date range: 'From' must be earlier than 'To'.");
        }

        var rangeRequest = new ReportRangeRequest(request.From, request.To);
        var totals = await _queryService.GetTotalsAsync(rangeRequest, cancellationToken).ConfigureAwait(false);

        var billedTotal = new Money(totals.BilledSalesTotal);
        var cashCollected = new Money(totals.SaleCashCollected);
        var digitalCollected = new Money(totals.SaleDigitalCollected);
        var settlementTotal = new Money(totals.ReceivableSettlementTotal);
        var countedCash = new Money(request.CountedCash.Value);

        var closing = new CashClosing(
            request.OperatorId,
            billedTotal,
            cashCollected,
            digitalCollected,
            settlementTotal,
            countedCash);

        await _cashClosingRepository.AddAsync(closing, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var summary = new CashClosingSummary(
            BilledTotal: totals.BilledSalesTotal,
            CashCollected: totals.SaleCashCollected,
            DigitalCollected: totals.SaleDigitalCollected,
            ReceivableSettlementTotal: totals.ReceivableSettlementTotal,
            ExpectedDrawerCash: totals.ExpectedDrawerCash,
            CountedCash: request.CountedCash.Value,
            Difference: request.CountedCash.Value - totals.ExpectedDrawerCash);

        return new CashClosingResult(true, summary, null);
    }
}
