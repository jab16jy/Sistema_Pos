using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Audit;
using POS.Application.UseCases.Audit;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class AuditLogViewModel : ViewModelBase
{
    private const string AllFilterLabel = "Todas";
    private readonly GetAuditLogsUseCase _getAuditLogs;
    private string _message = "Carga los últimos movimientos del sistema.";
    private string _usernameFilter = string.Empty;
    private string _selectedActionFilter = AllFilterLabel;
    private string _selectedEntityFilter = AllFilterLabel;

    public AuditLogViewModel(GetAuditLogsUseCase getAuditLogs)
    {
        _getAuditLogs = getAuditLogs;
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar auditoría: {ex.Message}"));
        ClearFiltersCommand = new RelayCommand(_ => RunSafe(ClearFiltersAsync(), ex => Message = $"Error al limpiar filtros: {ex.Message}"));
    }

    public ObservableCollection<AuditLogEntry> Logs { get; } = [];
    public IReadOnlyList<string> AvailableActionFilters { get; } = [AllFilterLabel, "Added", "Modified", "Deleted"];
    public IReadOnlyList<string> AvailableEntityFilters { get; } = [AllFilterLabel, "User", "Sale", "Product", "CashierShift", "Customer"];

    public ICommand LoadCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    public string UsernameFilter
    {
        get => _usernameFilter;
        set => SetProperty(ref _usernameFilter, value);
    }

    public string SelectedActionFilter
    {
        get => _selectedActionFilter;
        set => SetProperty(ref _selectedActionFilter, value);
    }

    public string SelectedEntityFilter
    {
        get => _selectedEntityFilter;
        set => SetProperty(ref _selectedEntityFilter, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var filter = BuildFilter();
            var logs = await _getAuditLogs.ExecuteAsync(100, filter, ct).ConfigureAwait(true);
            Logs.Clear();
            foreach (var log in logs)
            {
                Logs.Add(log);
            }

            Message = Logs.Count == 0
                ? "Aún no hay eventos de auditoría para los filtros seleccionados."
                : $"Mostrando {Logs.Count} evento(s).";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar auditoría: {ex.Message}";
        }
    }

    private async Task ClearFiltersAsync()
    {
        UsernameFilter = string.Empty;
        SelectedActionFilter = AllFilterLabel;
        SelectedEntityFilter = AllFilterLabel;
        await LoadAsync().ConfigureAwait(true);
    }

    private AuditLogFilter BuildFilter() => new(
        NormalizeTextFilter(UsernameFilter),
        NormalizeOptionFilter(SelectedActionFilter),
        NormalizeOptionFilter(SelectedEntityFilter));

    private static string? NormalizeTextFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptionFilter(string? value)
        => string.IsNullOrWhiteSpace(value) || string.Equals(value, AllFilterLabel, StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Trim();
}
