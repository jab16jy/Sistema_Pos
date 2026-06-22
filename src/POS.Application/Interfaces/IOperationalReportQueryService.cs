namespace POS.Application.Interfaces;

/// <summary>
/// Stub interface for operational reporting (cash closing, receivables totals, etc.).
/// Implementation is WU4 scope — this stub allows ViewModel wiring in WU3 without coupling.
/// </summary>
public interface IOperationalReportQueryService
{
    // WU4 will add: GetCashClosingReportAsync, GetReceivablesTotalAsync, etc.
}
