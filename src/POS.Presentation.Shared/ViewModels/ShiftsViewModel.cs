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

    public ShiftsViewModel(StartShiftUseCase startShift, CloseShiftUseCase closeShift, GetShiftsUseCase getShifts, ICurrentOperatorSession session)
    {
        _startShift = startShift;
        _closeShift = closeShift;
        _getShifts = getShifts;
        _session = session;
        LoadCommand = new RelayCommand(_ => _ = LoadAsync());
        StartShiftCommand = new RelayCommand(_ => _ = StartAsync());
        CloseShiftCommand = new RelayCommand(_ => _ = CloseAsync(), _ => SelectedShift?.Status == ShiftStatus.Open);
    }

    public ObservableCollection<ShiftSummary> Shifts { get; } = [];
    public ICommand LoadCommand { get; }
    public ICommand StartShiftCommand { get; }
    public ICommand CloseShiftCommand { get; }

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

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var shifts = await _getShifts.ExecuteAsync(50, ct).ConfigureAwait(true);
        Shifts.Clear();
        foreach (var shift in shifts)
        {
            Shifts.Add(shift);
        }

        Message = Shifts.Count == 0 ? "Aún no hay turnos registrados." : $"Mostrando {Shifts.Count} turno(s).";
    }

    private async Task StartAsync()
    {
        try
        {
            await _startShift.ExecuteAsync(new StartShiftRequest(_session.OperatorId, _session.Username, OpeningCash, Notes));
            OpeningCash = 0m;
            Notes = string.Empty;
            Message = "Turno abierto correctamente.";
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    private async Task CloseAsync()
    {
        if (SelectedShift is null)
        {
            Message = "Selecciona un turno abierto para cerrarlo.";
            return;
        }

        try
        {
            await _closeShift.ExecuteAsync(new CloseShiftRequest(SelectedShift.Id, ClosingCash, Notes));
            ClosingCash = 0m;
            Notes = string.Empty;
            Message = "Turno cerrado correctamente.";
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }
}
