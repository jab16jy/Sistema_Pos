namespace POS.Application.DTOs.Reporting;

/// <summary>
/// Input to CloseCashRegisterUseCase.
/// CountedCash is the physical cash the operator counted in the drawer.
/// A null CountedCash is rejected — closing requires the physical count.
/// </summary>
public sealed record CashClosingRequest(
    DateTimeOffset From,
    DateTimeOffset To,
    decimal? CountedCash);
