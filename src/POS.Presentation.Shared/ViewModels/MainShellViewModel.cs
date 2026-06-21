using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Sales;
using POS.Presentation.Shared.Commands;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.Shell;

namespace POS.Presentation.Shared.ViewModels;

public sealed class MainShellViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
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
    {
        Cashier = cashier ?? throw new ArgumentNullException(nameof(cashier));
        _dashboardSummaryUseCase = dashboardSummaryUseCase;
        Modules = new ObservableCollection<ModuleNavigationItem>
        {
            new("cashier", "Caja", "Escanea productos, cobra y confirma ventas.", "F2", true),
            new("products", "Productos", "Registra catálogo, precios, stock mínimo y códigos.", "F3", false),
            new("inventory", "Inventario", "Consulta existencias, bajo stock y movimientos.", "F4", false),
            new("customers", "Clientes", "Busca clientes, historial y puntos.", "F5", false),
            new("quotes", "Cotizaciones", "Crear y gestionar cotizaciones de clientes", "Alt+Q", IsImplemented: true),
            new("suppliers", "Proveedores", "Gestiona proveedores y compras.", "F6", false),
            new("invoices", "Facturas", "Reimprime recibos y consulta devoluciones.", "F7", false),
            new("reports", "Reportes", "Revisa ventas, caja y utilidades.", "F8", false),
            new("settings", "Configuración", "Negocio, impresora, backup y moneda.", "F9", false)
        };

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
    }

    public ObservableCollection<ModuleNavigationItem> Modules { get; }

    public ObservableCollection<DashboardMetric> Metrics { get; }

    public CashierViewModel Cashier { get; }

    /// <summary>Quote history ViewModel for the Avalonia quotes module content area.
    /// Null in the prototype default constructor; injected by DI in App.axaml.cs.</summary>
    public QuoteHistoryViewModel? QuoteHistory { get; init; }

    public ICommand SelectModuleCommand { get; }

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
                OnPropertyChanged(nameof(IsQuotesSelected));
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

    public bool IsQuotesSelected => SelectedModule.Key == "quotes";

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
        OperatorMessage = module.IsImplemented
            ? "Módulo Caja activo como prototipo funcional. Valida carrito y pago sin guardar ventas todavía."
            : $"{module.Title} está planificado. Primero terminaremos el flujo mínimo de caja sin romper arquitectura.";
    }

    private static string FormatCurrency(decimal amount) => $"${amount.ToString("N0", ColombianCulture)}";
}
