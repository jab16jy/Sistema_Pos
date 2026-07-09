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
    private static readonly IReadOnlyDictionary<string, string> ActionLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Added"] = "Creado",
        ["Modified"] = "Actualizado",
        ["Deleted"] = "Eliminado"
    };

    private static readonly IReadOnlyDictionary<string, string> EntityLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Customer"] = "Cliente",
        ["Quote"] = "Cotización",
        ["QuoteItem"] = "Producto de cotización",
        ["Product"] = "Producto",
        ["InventoryMovement"] = "Movimiento de inventario",
        ["Sale"] = "Venta",
        ["SaleItem"] = "Producto vendido",
        ["SalePayment"] = "Pago de venta",
        ["SaleReturn"] = "Devolución",
        ["CashierShift"] = "Turno de caja",
        ["CashClosing"] = "Cierre de caja",
        ["User"] = "Usuario",
        ["Supplier"] = "Proveedor",
        ["Purchase"] = "Compra",
        ["PurchaseItem"] = "Producto comprado",
        ["Receivable"] = "Cuenta por cobrar",
        ["ReceivableSettlement"] = "Abono a cuenta por cobrar",
        ["LoyaltyAccount"] = "Cuenta de puntos",
        ["LoyaltyTransaction"] = "Movimiento de puntos",
        ["LoyaltyRule"] = "Regla de puntos",
        ["Category"] = "Categoría",
        ["SubCategory"] = "Subcategoría",
        ["UnitOfMeasure"] = "Unidad de medida",
        ["TaxProfile"] = "Perfil de impuesto",
        ["LoyaltyTier"] = "Nivel de fidelidad",
        ["ProductSupplier"] = "Proveedor de producto",
        ["ProductImportBatch"] = "Importación de productos",
        ["ProductImportLine"] = "Línea de importación",
        ["AppSettings"] = "Configuración",
        ["DocumentSendLog"] = "Envío de documento"
    };

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

    public ObservableCollection<AuditLogDisplayEntry> Logs { get; } = [];
    public IReadOnlyList<string> AvailableActionFilters { get; } = [AllFilterLabel, "Creado", "Actualizado", "Eliminado"];
    public IReadOnlyList<string> AvailableEntityFilters { get; } =
    [
        AllFilterLabel,
        "Cliente",
        "Cotización",
        "Producto",
        "Movimiento de inventario",
        "Venta",
        "Usuario",
        "Turno de caja",
        "Proveedor",
        "Compra",
        "Cuenta por cobrar",
        "Movimiento de puntos",
        "Regla de puntos",
        "Configuración",
        "Envío de documento"
    ];

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
                Logs.Add(ToDisplayEntry(log));
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
        NormalizeActionFilter(SelectedActionFilter),
        NormalizeEntityFilter(SelectedEntityFilter));

    private static string? NormalizeTextFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeActionFilter(string? value)
        => NormalizeMappedFilter(value, ActionLabels);

    private static string? NormalizeEntityFilter(string? value)
        => NormalizeMappedFilter(value, EntityLabels);

    private static string? NormalizeMappedFilter(string? value, IReadOnlyDictionary<string, string> labels)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, AllFilterLabel, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (labels.ContainsKey(trimmed))
        {
            return trimmed;
        }

        foreach (var (raw, label) in labels)
        {
            if (string.Equals(trimmed, label, StringComparison.OrdinalIgnoreCase))
            {
                return raw;
            }
        }

        return trimmed;
    }

    private static AuditLogDisplayEntry ToDisplayEntry(AuditLogEntry log)
    {
        string actionLabel = ActionLabels.TryGetValue(log.Action, out string? action) ? action : log.Action;
        string entityLabel = EntityLabels.TryGetValue(log.EntityName, out string? entity) ? entity : log.EntityName;
        string details = BuildDetails(log, actionLabel, entityLabel);

        return new AuditLogDisplayEntry(
            log.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            log.Username,
            actionLabel,
            entityLabel,
            details,
            log.EntityId);
    }

    private static string BuildDetails(AuditLogEntry log, string actionLabel, string entityLabel)
    {
        string baseText = string.IsNullOrWhiteSpace(log.Details)
            ? $"{entityLabel}: {actionLabel.ToLowerInvariant()}."
            : TranslateDetails(log.Details);

        // Solo mostramos una referencia que el administrador reconoce (cédula, N° de factura/cotización);
        // ocultamos los GUID técnicos y cualquier dato que el detalle ya nombre.
        if (string.IsNullOrWhiteSpace(log.EntityId)
            || Guid.TryParse(log.EntityId, out _)
            || baseText.Contains(log.EntityId, StringComparison.OrdinalIgnoreCase))
        {
            return baseText;
        }

        return $"{baseText} Ref.: {log.EntityId}.";
    }

    private static string TranslateDetails(string details)
    {
        return details
            .Replace("Record created.", "Registro creado.", StringComparison.OrdinalIgnoreCase)
            .Replace("Registro creado.", "Registro creado.", StringComparison.OrdinalIgnoreCase)
            .Replace("Record deleted.", "Registro eliminado.", StringComparison.OrdinalIgnoreCase)
            .Replace("Registro eliminado.", "Registro eliminado.", StringComparison.OrdinalIgnoreCase)
            .Replace("Record updated.", "Registro actualizado.", StringComparison.OrdinalIgnoreCase)
            .Replace("Registro actualizado.", "Registro actualizado.", StringComparison.OrdinalIgnoreCase)
            .Replace("Modified fields:", "Campos modificados:", StringComparison.OrdinalIgnoreCase)
            .Replace("Campos modificados:", "Campos modificados:", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record AuditLogDisplayEntry(
    string DateText,
    string Username,
    string ActionLabel,
    string EntityLabel,
    string Details,
    string? EntityId);
