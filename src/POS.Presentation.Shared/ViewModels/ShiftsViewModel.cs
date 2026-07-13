using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Shifts;
using POS.Application.Interfaces;
using POS.Application.UseCases.Shifts;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using System.Globalization;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class ShiftsViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    private readonly StartShiftUseCase _startShift;
    private readonly CloseShiftUseCase _closeShift;
    private readonly GetShiftsUseCase _getShifts;
    private readonly GetShiftSalesSummaryUseCase _getShiftSalesSummary;
    private readonly ICurrentOperatorSession _session;
    private ShiftSummary? _selectedShift;
    private ShiftRowPresentation? _selectedShiftRow;
    private decimal _openingCash;
    private decimal _closingCash;
    private string _notes = string.Empty;
    private string _message = "Carga los turnos o abre uno nuevo para el cajero actual.";
    private string _openNotes = string.Empty;
    private string _closeNotes = string.Empty;
    private string _selectedCashierUsername = string.Empty;
    private decimal _countedCash;
    private bool _isCountedCashAutoFilled;
    private ShiftSalesSummaryData _activeShiftSalesSummary = ShiftSalesSummaryData.Empty;
    private DateTime? _fromDate;
    private DateTime? _toDate;

    public ShiftsViewModel(
        StartShiftUseCase startShift,
        CloseShiftUseCase closeShift,
        GetShiftsUseCase getShifts,
        GetShiftSalesSummaryUseCase getShiftSalesSummary,
        ICurrentOperatorSession session)
    {
        _startShift = startShift;
        _closeShift = closeShift;
        _getShifts = getShifts;
        _getShiftSalesSummary = getShiftSalesSummary;
        _session = session;
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar turnos: {ex.Message}"));
        StartShiftCommand = new RelayCommand(_ => RunSafe(StartAsync(), ex => Message = $"Error al abrir turno: {ex.Message}"));
        CloseShiftCommand = new RelayCommand(_ => RunSafe(CloseAsync(), ex => Message = $"Error al cerrar turno: {ex.Message}"), _ => HasActiveShift);
        ExportCommand = new RelayCommand(_ => Message = "Exportacion de turnos no disponible en este entorno.");
        ApplyDateFilterCommand = new RelayCommand(_ => ApplyDateFilter());

        _selectedCashierUsername = session.Username;
    }

    public ObservableCollection<ShiftSummary> Shifts { get; } = [];
    public ObservableCollection<ShiftRowPresentation> FilteredShifts { get; } = [];
    public ICommand LoadCommand { get; }
    public ICommand StartShiftCommand { get; }
    public ICommand CloseShiftCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ApplyDateFilterCommand { get; }

    public ShiftSummary? SelectedShift
    {
        get => _selectedShift;
        set
        {
            if (SetProperty(ref _selectedShift, value) && CloseShiftCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>The row the grid actually highlights — bound as the ListBox's SelectedItem.</summary>
    public ShiftRowPresentation? SelectedShiftRow
    {
        get => _selectedShiftRow;
        set => SetProperty(ref _selectedShiftRow, value);
    }

    public decimal OpeningCash
    {
        get => _openingCash;
        set => SetProperty(ref _openingCash, value);
    }

    public decimal ClosingCash
    {
        get => _closingCash;
        set => SetProperty(ref _closingCash, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    // ── Presentation-only: Active shift banner ──

    public bool HasActiveShift
    {
        get => Shifts.Any(s => s.Status == ShiftStatus.Open);
    }

    public string ActiveShiftCashier
    {
        get
        {
            var active = Shifts.FirstOrDefault(s => s.Status == ShiftStatus.Open);
            return active?.CashierUsername ?? "—";
        }
    }

    public string ActiveShiftOpenTime
    {
        get
        {
            var active = Shifts.FirstOrDefault(s => s.Status == ShiftStatus.Open);
            return active is not null ? active.OpenedAt.ToString("HH:mm") : "—";
        }
    }

    public string ActiveShiftBaseCashFormatted
    {
        get
        {
            var active = Shifts.FirstOrDefault(s => s.Status == ShiftStatus.Open);
            return active is not null ? $"${active.OpeningCash.ToString("N0", ColombianCulture)}" : "—";
        }
    }

    public string ActiveShiftSalesTotal
    {
        get => $"${_activeShiftSalesSummary.SalesTotal.ToString("N0", ColombianCulture)}";
    }

    // ── Presentation-only: Open shift panel ──

    public string SelectedCashierUsername
    {
        get => _selectedCashierUsername;
        set => SetProperty(ref _selectedCashierUsername, value ?? string.Empty);
    }

    public string OpenNotes
    {
        get => _openNotes;
        set => SetProperty(ref _openNotes, value ?? string.Empty);
    }

    // ── Presentation-only: Close shift panel ──

    public decimal CountedCash
    {
        get => _countedCash;
        set
        {
            if (SetProperty(ref _countedCash, value))
            {
                _isCountedCashAutoFilled = false;
                OnPropertyChanged(nameof(CloseDifference));
                OnPropertyChanged(nameof(CloseDifferenceLabel));
                OnPropertyChanged(nameof(CloseDifferenceKind));
                OnPropertyChanged(nameof(HasCloseDifference));
            }
        }
    }

    public string CloseNotes
    {
        get => _closeNotes;
        set => SetProperty(ref _closeNotes, value ?? string.Empty);
    }

    public decimal ExpectedCash
    {
        get
        {
            var active = Shifts.FirstOrDefault(s => s.Status == ShiftStatus.Open);
            return active is null ? 0m : active.OpeningCash + _activeShiftSalesSummary.CashPayments;
        }
    }

    public string ExpectedCashFormatted => ExpectedCash > 0 ? $"${ExpectedCash.ToString("N0", ColombianCulture)}" : "—";

    public string ExpectedDigitalFormatted => $"${_activeShiftSalesSummary.DigitalPayments.ToString("N0", ColombianCulture)}";

    public decimal CloseDifference => CountedCash - ExpectedCash;

    public string CloseDifferenceLabel
    {
        get
        {
            if (CountedCash == 0 && ExpectedCash == 0) return string.Empty;
            var diff = CloseDifference;
            if (diff == 0) return "Cuadra";
            return diff > 0 ? $"+${Math.Abs(diff).ToString("N0", ColombianCulture)}" : $"-${Math.Abs(diff).ToString("N0", ColombianCulture)}";
        }
    }

    public string CloseDifferenceKind
    {
        get
        {
            if (CountedCash == 0 && ExpectedCash == 0) return "neutral";
            var diff = CloseDifference;
            if (diff == 0) return "success";
            return diff > 0 ? "primary" : "error";
        }
    }

    public bool HasCloseDifference => CountedCash > 0 || ExpectedCash > 0;

    // ── Presentation-only: Date filter ──

    public DateTime? FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var shifts = await _getShifts.ExecuteAsync(50, ct).ConfigureAwait(true);
            Shifts.Clear();
            foreach (var shift in shifts)
            {
                Shifts.Add(shift);
            }

            await LoadActiveShiftSalesSummaryAsync(ct).ConfigureAwait(true);
            ApplyDateFilter();
            RefreshActiveShiftProperties();

            Message = Shifts.Count == 0 ? "Aun no hay turnos registrados." : $"Mostrando {Shifts.Count} turno(s).";

            if (CloseShiftCommand is RelayCommand cmd)
            {
                cmd.RaiseCanExecuteChanged();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar turnos: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads and selects a specific shift by id, widening the date filter so it's guaranteed
    /// visible regardless of whatever range was previously applied. Used by the "Ir a Turnos"
    /// redirect from Reportes' pending-shifts list. Sets <see cref="SelectedShiftRow"/> (not just
    /// <see cref="SelectedShift"/>) because the grid binds to <see cref="FilteredShifts"/>, whose
    /// items are <see cref="ShiftRowPresentation"/>, a different type than <see cref="ShiftSummary"/>.
    /// </summary>
    public async Task SelectShiftAsync(Guid shiftId, CancellationToken ct = default)
    {
        FromDate = null;
        ToDate = null;
        await LoadAsync(ct).ConfigureAwait(true);
        SelectedShift = Shifts.FirstOrDefault(s => s.Id == shiftId);
        SelectedShiftRow = FilteredShifts.FirstOrDefault(r => r.Id == shiftId);
    }

    private void ApplyDateFilter()
    {
        FilteredShifts.Clear();
        var filtered = Shifts.AsEnumerable();

        if (FromDate.HasValue)
        {
            // FromDate is Kind=Local; DateTimeOffset(local, Zero) throws when the local offset isn't 0.
            // Use the real local offset, same as ReportsViewModel does.
            var from = new DateTimeOffset(FromDate.Value.Date, TimeZoneInfo.Local.GetUtcOffset(FromDate.Value.Date));
            filtered = filtered.Where(s => s.OpenedAt >= from);
        }

        if (ToDate.HasValue)
        {
            var toDay = ToDate.Value.Date.AddDays(1);
            var to = new DateTimeOffset(toDay, TimeZoneInfo.Local.GetUtcOffset(toDay));
            filtered = filtered.Where(s => s.OpenedAt < to);
        }

        foreach (var shift in filtered)
        {
            FilteredShifts.Add(ToShiftRow(shift));
        }
    }

    /// <summary>
    /// Maps a <see cref="ShiftSummary"/> into its presentation row, including difference coloring.
    /// Internal so <see cref="ReportsViewModel"/> can reuse the same convention for the read-only
    /// Cierres y arqueos subtab (13.6) instead of duplicating the mapping logic.
    /// </summary>
    public static ShiftRowPresentation ToShiftRow(ShiftSummary s)
    {
        var statusLabel = s.Status == ShiftStatus.Open ? "Abierto" : "Cerrado";
        var statusChipKind = s.Status == ShiftStatus.Open ? "success" : "neutral";
        var openedFormatted = s.OpenedAt.ToString("yyyy-MM-dd HH:mm");
        var closedFormatted = s.ClosedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        var openingFormatted = $"${s.OpeningCash.ToString("N0", ColombianCulture)}";
        var closingFormatted = s.ClosingCash.HasValue ? $"${s.ClosingCash.Value.ToString("N0", ColombianCulture)}" : "—";
        var salesTotalFormatted = s.HasClosingSummary ? $"${s.SalesTotal.ToString("N0", ColombianCulture)}" : "—";
        var digitalPaymentsFormatted = s.HasClosingSummary ? $"Digital: ${s.DigitalPayments.ToString("N0", ColombianCulture)}" : string.Empty;
        var expectedCashFormatted = s.HasClosingSummary ? $"${s.ExpectedCash.ToString("N0", ColombianCulture)}" : "—";

        string differenceLabel;
        string differenceKind;

        if (s.Status == ShiftStatus.Open)
        {
            differenceLabel = "—";
            differenceKind = "neutral";
        }
        else if (!s.HasClosingSummary)
        {
            differenceLabel = "—";
            differenceKind = "neutral";
        }
        else
        {
            var diff = (s.ClosingCash ?? 0m) - s.ExpectedCash;
            if (diff == 0)
            {
                differenceLabel = "Cuadra";
                differenceKind = "success";
            }
            else if (diff > 0)
            {
                differenceLabel = $"+${Math.Abs(diff).ToString("N0", ColombianCulture)}";
                differenceKind = "primary";
            }
            else
            {
                differenceLabel = $"-${Math.Abs(diff).ToString("N0", ColombianCulture)}";
                differenceKind = "error";
            }
        }

        return new ShiftRowPresentation(
            s.Id,
            s.CashierUsername,
            statusLabel,
            statusChipKind,
            openedFormatted,
            closedFormatted,
            openingFormatted,
            closingFormatted,
            salesTotalFormatted,
            digitalPaymentsFormatted,
            expectedCashFormatted,
            differenceLabel,
            differenceKind,
            s.Status == ShiftStatus.Open);
    }

    private void RefreshActiveShiftProperties()
    {
        OnPropertyChanged(nameof(HasActiveShift));
        OnPropertyChanged(nameof(ActiveShiftCashier));
        OnPropertyChanged(nameof(ActiveShiftOpenTime));
        OnPropertyChanged(nameof(ActiveShiftBaseCashFormatted));
        OnPropertyChanged(nameof(ActiveShiftSalesTotal));
        OnPropertyChanged(nameof(ExpectedCash));
        OnPropertyChanged(nameof(ExpectedCashFormatted));
        OnPropertyChanged(nameof(ExpectedDigitalFormatted));
        OnPropertyChanged(nameof(CloseDifference));
        OnPropertyChanged(nameof(CloseDifferenceLabel));
        OnPropertyChanged(nameof(CloseDifferenceKind));
        OnPropertyChanged(nameof(HasCloseDifference));
    }

    private async Task LoadActiveShiftSalesSummaryAsync(CancellationToken ct)
    {
        var activeShift = Shifts.FirstOrDefault(shift => shift.Status == ShiftStatus.Open);
        _activeShiftSalesSummary = activeShift is null
            ? ShiftSalesSummaryData.Empty
            : await _getShiftSalesSummary.ExecuteAsync(activeShift.CashierUserId, activeShift.OpenedAt, ct).ConfigureAwait(true);

        SetAutoCountedCash(ExpectedCash);
    }

    private void SetAutoCountedCash(decimal amount)
    {
        CountedCash = amount;
        _isCountedCashAutoFilled = true;
    }

    private async Task StartAsync()
    {
        try
        {
            var cashierName = string.IsNullOrWhiteSpace(SelectedCashierUsername)
                ? _session.Username
                : SelectedCashierUsername;

            await _startShift.ExecuteAsync(new StartShiftRequest(_session.OperatorId, cashierName, OpeningCash, OpenNotes));
            OpeningCash = 0m;
            OpenNotes = string.Empty;
            Message = "Turno abierto exitosamente";
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Error al abrir turno: {ex.Message}";
        }
    }

    private async Task CloseAsync()
    {
        if (!HasActiveShift)
        {
            Message = "No hay un turno activo para cerrar.";
            return;
        }

        if (CountedCash < 0)
        {
            Message = "El efectivo contado no puede ser negativo.";
            return;
        }

        var activeShift = Shifts.FirstOrDefault(s => s.Status == ShiftStatus.Open);
        if (activeShift is null)
        {
            Message = "No se encontro un turno abierto.";
            return;
        }

        try
        {
            _activeShiftSalesSummary = await _getShiftSalesSummary
                .ExecuteAsync(activeShift.CashierUserId, activeShift.OpenedAt)
                .ConfigureAwait(true);
            RefreshActiveShiftProperties();

            if (_isCountedCashAutoFilled)
            {
                SetAutoCountedCash(ExpectedCash);
            }

            await _closeShift.ExecuteAsync(new CloseShiftRequest(activeShift.Id, CountedCash, CloseNotes));
            SetAutoCountedCash(0m);
            CloseNotes = string.Empty;
            Message = "Turno cerrado · Reporte generado";
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Error al cerrar turno: {ex.Message}";
        }
    }
}

/// <summary>Presentation-only shift row with formatted fields and difference coloring.</summary>
public sealed record ShiftRowPresentation(
    Guid Id,
    string CashierUsername,
    string StatusLabel,
    string StatusChipKind,
    string OpenedAtFormatted,
    string ClosedAtFormatted,
    string OpeningCashFormatted,
    string ClosingCashFormatted,
    string SalesTotalFormatted,
    string DigitalPaymentsFormatted,
    string ExpectedCashFormatted,
    string DifferenceLabel,
    string DifferenceKind,
    bool IsOpen);
