using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Audit;
using POS.Application.UseCases.Audit;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class AuditLogViewModel : ViewModelBase
{
    private readonly GetAuditLogsUseCase _getAuditLogs;
    private string _message = "Carga los últimos movimientos del sistema.";

    public AuditLogViewModel(GetAuditLogsUseCase getAuditLogs)
    {
        _getAuditLogs = getAuditLogs;
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar auditoría: {ex.Message}"));
    }

    public ObservableCollection<AuditLogEntry> Logs { get; } = [];
    public ICommand LoadCommand { get; }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var logs = await _getAuditLogs.ExecuteAsync(100, ct).ConfigureAwait(true);
            Logs.Clear();
            foreach (var log in logs)
            {
                Logs.Add(log);
            }

            Message = Logs.Count == 0 ? "Aún no hay eventos de auditoría." : $"Mostrando {Logs.Count} evento(s).";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar auditoría: {ex.Message}";
        }
    }
}
