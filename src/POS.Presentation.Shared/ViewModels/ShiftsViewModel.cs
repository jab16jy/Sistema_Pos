using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Shifts;
using POS.Application.Interfaces;
using POS.Application.UseCases.Shifts;
using POS.Domain.Enums;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class ShiftsViewModel : ViewModelBase
{
    private readonly StartShiftUseCase _startShift;
    private readonly CloseShiftUseCase _closeShift;
    private readonly GetShiftsUseCase _getShifts;
    private readonly ICurrentOperatorSession _session;
    private ShiftSummary? _selectedShift;
    private decimal _openingCash;
    private decimal _closingCash;
    private string _notes = string.Empty;
    private string _message = "Carga los turnos o abre uno nuevo para el cajero actual.";
    private string _openNotes = string.Empty;
    private string _closeNotes = string.Empty;
    private string _selectedCashierUsername = string.Empty;
    private decimal _countedCash;
    private DateTime? _fromDate;
    private DateTime? _toDate;

    public ShiftsViewModel(StartShiftUseCase startShift, CloseShiftUseCase closeShift, GetShiftsUseCase getShifts, ICurrentOperatorSession session)
    {
        _startShift = startShift;
        _closeShift = closeShift;
        _getShifts = getShifts;
        _session = session;
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar turnos: {ex.Message}"));
        StartShiftCommand = new RelayCommand(_ => RunSafe(StartAsync(), ex => Message = $"Error al abrir turno: {ex.Message}"));
        CloseShiftCommand = new RelayCommand(_ => RunSafe(CloseAsync(), ex => Message = $"Error al cerrar turno: {ex.Message}"), _ => HasActiveShift);
        ExportCommand = new RelayCommand(_ => Message = "Exportacion de turnos no disponible en este entorno.");
        ApplyDateFilterCommand = new RelayCommand(_ => ApplyDateFilter());

        _fromDate = DateTime.Today;
        _toDate = DateTime.Today;
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
            return active is not null ? $"${active.OpeningCash:N0}" : "—";
        }
    }

    public string ActiveShiftSalesTotal
    {
        get => "—";
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
            return active?.OpeningCash ?? 0m;
        }
    }

    public string ExpectedCashFormatted => ExpectedCash > 0 ? $"${ExpectedCash:N0}" : "—";

    public string ExpectedDigitalFormatted => "—";

    public decimal CloseDifference => CountedCash - ExpectedCash;

    public string CloseDifferenceLabel
    {
        get
        {
            if (CountedCash == 0 && ExpectedCash == 0) return string.Empty;
            var diff = CloseDifference;
            if (diff == 0) return "Cuadra";
            return diff > 0 ? $"+${Math.Abs(diff):N0}" : $"-${Math.Abs(diff):N0}";
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
    internal static ShiftRowPresentation ToShiftRow(ShiftSummary s)
    {
        var statusLabel = s.Status == ShiftStatus.Open ? "Abierto" : "Cerrado";
        var statusChipKind = s.Status == ShiftStatus.Open ? "success" : "neutral";
        var openedFormatted = s.OpenedAt.ToString("yyyy-MM-dd HH:mm");
        var closedFormatted = s.ClosedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        var openingFormatted = $"${s.OpeningCash:N0}";
        var closingFormatted = s.ClosingCash.HasValue ? $"${s.ClosingCash.Value:N0}" : "—";

        string differenceLabel;
        string differenceKind;

        if (s.Status == ShiftStatus.Open)
        {
            differenceLabel = "—";
            differenceKind = "neutral";
        }
        else
        {
            var diff = (s.ClosingCash ?? 0m) - s.OpeningCash;
            if (diff == 0)
            {
                differenceLabel = "Cuadra";
                differenceKind = "success";
            }
            else if (diff > 0)
            {
                differenceLabel = $"+${Math.Abs(diff):N0}";
                differenceKind = "primary";
            }
            else
            {
                differenceLabel = $"-${Math.Abs(diff):N0}";
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

        if (CountedCash <= 0)
        {
            Message = "Ingresa el efectivo contado";
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
            await _closeShift.ExecuteAsync(new CloseShiftRequest(activeShift.Id, CountedCash, CloseNotes));
            CountedCash = 0m;
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
    string DifferenceLabel,
    string DifferenceKind,
    bool IsOpen);
