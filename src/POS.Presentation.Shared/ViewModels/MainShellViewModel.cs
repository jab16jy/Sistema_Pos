using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Sales;
using POS.Domain.Enums;
using POS.Presentation.Shared.Commands;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.Shell;

namespace POS.Presentation.Shared.ViewModels;

public sealed class MainShellViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private static readonly IReadOnlySet<string> CashierModuleKeys = new HashSet<string>
    {
        "cashier", "shifts", "customers", "quotes", "invoices"
    };

    private readonly GetDashboardSummaryUseCase? _dashboardSummaryUseCase;
    private ModuleNavigationItem _selectedModule;
    private string _operatorMessage;

    public MainShellViewModel()
        : this(new CashierViewModel(new PrototypeProductLookupService()))
    {
    }

    public MainShellViewModel(CashierViewModel cashier)
        : this(cashier, null)
    {
    }

    public MainShellViewModel(CashierViewModel cashier, GetDashboardSummaryUseCase? dashboardSummaryUseCase)
        : this(cashier, dashboardSummaryUseCase, UserRole.Administrator, string.Empty)
    {
    }

    public MainShellViewModel(CashierViewModel cashier, GetDashboardSummaryUseCase? dashboardSummaryUseCase, UserRole role, string username)
    {
        Cashier = cashier ?? throw new ArgumentNullException(nameof(cashier));
        _dashboardSummaryUseCase = dashboardSummaryUseCase;
        Role = role;
        OperatorDisplayName = BuildDisplayName(username, role);

        var allModules = new[]
        {
            new ModuleNavigationItem("cashier", "Caja", "Escanea productos, cobra y confirma ventas.", "F2", true),
            new ModuleNavigationItem("shifts", "Turnos", "Abre y cierra turnos de cajeros.", "Alt+T", true),
            new ModuleNavigationItem("users", "Usuarios", "Crea y gestiona usuarios del sistema.", "F1", true),
            new ModuleNavigationItem("audit", "Auditoría", "Consulta quién cambió qué y cuándo.", "Alt+A", true),
            new ModuleNavigationItem("products", "Productos", "Registra catálogo, precios, stock mínimo y códigos.", "F3", true),
            new ModuleNavigationItem("inventory", "Inventario", "Consulta existencias, bajo stock y movimientos.", "F4", true),
            new ModuleNavigationItem("customers", "Clientes", "Busca clientes, historial y estado operativo.", "F5", true),
            new ModuleNavigationItem("quotes", "Cotizaciones", "Crear y gestionar cotizaciones de clientes", "Alt+Q", IsImplemented: true),
            new ModuleNavigationItem("suppliers", "Proveedores", "Gestiona proveedores y compras.", "F6", true),
            new ModuleNavigationItem("receivables", "Cuentas por Cobrar", "Consulta saldos pendientes y registra abonos.", "F7", true),
            new ModuleNavigationItem("invoices", "Facturas", "Reimprime recibos y consulta devoluciones.", "F8", true),
            new ModuleNavigationItem("reports", "Reportes", "Revisa ventas, caja y utilidades.", "F9", true),
            new ModuleNavigationItem("loyalty", "Puntos", "Consulta saldo y redime puntos de lealtad.", "F11", true),
            new ModuleNavigationItem("barcode", "Codigos de Barras", "Genera e imprime etiquetas con codigo de barras.", "F12", true),
            new ModuleNavigationItem("settings", "Configuración", "Negocio, impresora, backup y moneda.", "F10", true)
        };

        var visible = role == UserRole.Cashier
            ? allModules.Where(m => CashierModuleKeys.Contains(m.Key))
            : allModules;

        Modules = new ObservableCollection<ModuleNavigationItem>(visible);

        Metrics = new ObservableCollection<DashboardMetric>
        {
            new("Ventas del día", "$0", "Se conectará al caso de uso de venta."),
            new("Efectivo en caja", "$0", "Solo pagos en efectivo incrementan caja."),
            new("Pagos digitales", "$0", "Ingreso registrado, no efectivo físico."),
            new("Stock bajo", "0", "Se conectará al inventario SQLite.")
        };

        _selectedModule = Modules[0];
        _operatorMessage = "Escanea el código o escribe el nombre del producto. Este prototipo no guarda datos todavía.";
        SelectModuleCommand = new RelayCommand(SelectModule, CanSelectModule);
        LogoutCommand = new RelayCommand(_ => LogoutRequested?.Invoke());
    }

    public event Action? LogoutRequested;

    public UserRole Role { get; }
    public string OperatorDisplayName { get; }

    public ObservableCollection<ModuleNavigationItem> Modules { get; }

    public ObservableCollection<DashboardMetric> Metrics { get; }

    public CashierViewModel Cashier { get; }

    public QuoteViewModel? Quote { get; init; }

    public QuoteHistoryViewModel? QuoteHistory { get; init; }

    public CustomersViewModel? Customers { get; init; }

    public PurchasesViewModel? Purchases { get; init; }

    public ReceivablesViewModel? Receivables { get; init; }

    public ReportsViewModel? Reports { get; init; }

    public ProductsViewModel? Products { get; init; }

    public InventoryViewModel? Inventory { get; init; }

    public InvoicesViewModel? Invoices { get; init; }

    public SettingsViewModel? Settings { get; init; }

    public LoyaltyViewModel? Loyalty { get; init; }

    public BarcodeViewModel? Barcode { get; init; }

    public UserManagementViewModel? UserManagement { get; init; }

    public ShiftsViewModel? Shifts { get; init; }

    public AuditLogViewModel? AuditLog { get; init; }

    public ICommand SelectModuleCommand { get; }
    public ICommand LogoutCommand { get; }

    public ModuleNavigationItem SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (value is null)
            {
                return;
            }

            if (SetProperty(ref _selectedModule, value))
            {
                UpdateOperatorMessage(value);
                OnPropertyChanged(nameof(MainActionText));
                OnPropertyChanged(nameof(IsCashierSelected));
                OnPropertyChanged(nameof(IsShiftsSelected));
                OnPropertyChanged(nameof(IsUsersSelected));
                OnPropertyChanged(nameof(IsAuditSelected));
                OnPropertyChanged(nameof(IsProductsSelected));
                OnPropertyChanged(nameof(IsInventorySelected));
                OnPropertyChanged(nameof(IsQuotesSelected));
                OnPropertyChanged(nameof(IsCustomersSelected));
                OnPropertyChanged(nameof(IsPurchasesSelected));
                OnPropertyChanged(nameof(IsReceivablesSelected));
                OnPropertyChanged(nameof(IsInvoicesSelected));
                OnPropertyChanged(nameof(IsReportsSelected));
                OnPropertyChanged(nameof(IsLoyaltySelected));
                OnPropertyChanged(nameof(IsBarcodeSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
                OnPropertyChanged(nameof(IsPlannedModuleSelected));
            }
        }
    }

    public string OperatorMessage
    {
        get => _operatorMessage;
        private set => SetProperty(ref _operatorMessage, value);
    }

    public string MainActionText => IsCashierSelected ? "Cobrar" : "Abrir módulo";

    public bool IsCashierSelected => SelectedModule.Key == "cashier";

    public bool IsUsersSelected => SelectedModule.Key == "users";

    public bool IsShiftsSelected => SelectedModule.Key == "shifts";

    public bool IsAuditSelected => SelectedModule.Key == "audit";

    public bool IsProductsSelected => SelectedModule.Key == "products";

    public bool IsInventorySelected => SelectedModule.Key == "inventory";

    public bool IsQuotesSelected => SelectedModule.Key == "quotes";

    public bool IsCustomersSelected => SelectedModule.Key == "customers";

    public bool IsPurchasesSelected => SelectedModule.Key == "suppliers";

    public bool IsReceivablesSelected => SelectedModule.Key == "receivables";

    public bool IsInvoicesSelected => SelectedModule.Key == "invoices";

    public bool IsReportsSelected => SelectedModule.Key == "reports";

    public bool IsLoyaltySelected => SelectedModule.Key == "loyalty";

    public bool IsBarcodeSelected => SelectedModule.Key == "barcode";

    public bool IsSettingsSelected => SelectedModule.Key == "settings";

    public bool IsPlannedModuleSelected => !IsCashierSelected && !IsShiftsSelected && !IsUsersSelected && !IsAuditSelected && !IsProductsSelected && !IsInventorySelected && !IsQuotesSelected && !IsCustomersSelected && !IsPurchasesSelected && !IsReceivablesSelected && !IsInvoicesSelected && !IsReportsSelected && !IsLoyaltySelected && !IsBarcodeSelected && !IsSettingsSelected;

    public async Task LoadDashboardMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (_dashboardSummaryUseCase is null)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = new DashboardSummaryRequest(
            new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(now.Year, now.Month, now.Day, 23, 59, 59, TimeSpan.Zero));
        DashboardSummary summary = await _dashboardSummaryUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

        Metrics.Clear();
        Metrics.Add(new DashboardMetric("Ventas del día", FormatCurrency(summary.SalesTotal), $"{summary.SalesCount} ventas persistidas en el periodo."));
        Metrics.Add(new DashboardMetric("Efectivo en caja", FormatCurrency(summary.CashInDrawer), "Solo pagos en efectivo incrementan caja."));
        Metrics.Add(new DashboardMetric("Pagos digitales", FormatCurrency(summary.DigitalPayments), "Ingreso registrado, no efectivo físico."));
        Metrics.Add(new DashboardMetric("Stock bajo", summary.LowStockCount.ToString(CultureInfo.InvariantCulture), "Productos activos en o bajo mínimo."));

        OperatorMessage = summary.SalesCount == 0
            ? "No hay ventas persistidas para el periodo del dashboard."
            : $"{summary.SalesCount} ventas persistidas cargadas desde Application/SQLite.";
    }

    private static bool CanSelectModule(object? parameter) => parameter is ModuleNavigationItem;

    private void SelectModule(object? parameter)
    {
        if (parameter is not ModuleNavigationItem module)
        {
            return;
        }

        SelectedModule = module;
    }

    private void UpdateOperatorMessage(ModuleNavigationItem module)
    {
        OperatorMessage = module switch
        {
            { Key: "cashier", IsImplemented: true } => "Módulo Caja activo. Escanea productos, cobra y confirma ventas.",
            { Key: "shifts", IsImplemented: true } => "Módulo Turnos activo. Controla apertura y cierre por cajero.",
            { Key: "users", IsImplemented: true } => "Módulo Usuarios activo. Gestiona accesos y contraseñas.",
            { Key: "audit", IsImplemented: true } => "Módulo Auditoría activo. Revisa cambios y operaciones recientes.",
            { Key: "products", IsImplemented: true } => "Módulo Productos activo. Registra catálogo, precios, stock mínimo y códigos.",
            { Key: "inventory", IsImplemented: true } => "Módulo Inventario activo. Consulta existencias, bajo stock y movimientos.",
            { Key: "customers", IsImplemented: true } => "Módulo Clientes activo. Gestiona datos operativos e historial.",
            { Key: "quotes", IsImplemented: true } => "Módulo Cotizaciones activo. Crear, consultar y convertir cotizaciones.",
            { Key: "suppliers", IsImplemented: true } => "Módulo Proveedores activo. Registra proveedores y compras.",
            { Key: "receivables", IsImplemented: true } => "Módulo Cuentas por Cobrar activo. Consulta saldos pendientes y registra abonos.",
            { Key: "invoices", IsImplemented: true } => "Módulo Facturas activo. Reimprime recibos y procesa devoluciones.",
            { Key: "reports", IsImplemented: true } => "Módulo Reportes activo. Consulta totales operativos separados por efectivo, digital y abonos.",
            { Key: "loyalty", IsImplemented: true } => "Módulo Puntos activo. Consulta saldo y redime puntos de lealtad.",
            { Key: "barcode", IsImplemented: true } => "Módulo Códigos de Barras activo. Genera e imprime etiquetas.",
            { Key: "settings", IsImplemented: true } => "Módulo Configuración activo. Negocio, impresora, backup y moneda.",
            _ => $"{module.Title} está planificado. Primero terminaremos el flujo mínimo de caja sin romper arquitectura."
        };
    }

    private static string FormatCurrency(decimal amount) => $"${amount.ToString("N0", ColombianCulture)}";

    private static string BuildDisplayName(string username, UserRole role)
    {
        if (string.IsNullOrEmpty(username)) return string.Empty;
        var roleName = role == UserRole.Administrator ? "Administrador" : "Cajero";
        return $"{username} — {roleName}";
    }
}
