using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Presentation.Shared.Commands;
using POS.Presentation.Shared.Prototype;
using POS.Presentation.Shared.Shell;

namespace POS.Presentation.Shared.ViewModels;

public sealed class MainShellViewModel : ViewModelBase
{
    private ModuleNavigationItem _selectedModule;
    private string _operatorMessage;

    public MainShellViewModel()
        : this(new CashierViewModel(new PrototypeProductLookupService()))
    {
    }

    public MainShellViewModel(CashierViewModel cashier)
    {
        Cashier = cashier ?? throw new ArgumentNullException(nameof(cashier));
        Modules = new ObservableCollection<ModuleNavigationItem>
        {
            new("cashier", "Caja", "Escanea productos, cobra y confirma ventas.", "F2", true),
            new("products", "Productos", "Registra catálogo, precios, stock mínimo y códigos.", "F3", false),
            new("inventory", "Inventario", "Consulta existencias, bajo stock y movimientos.", "F4", false),
            new("customers", "Clientes", "Busca clientes, historial y puntos.", "F5", false),
            new("suppliers", "Proveedores", "Gestiona proveedores y compras.", "F6", false),
            new("invoices", "Facturas", "Reimprime recibos y consulta devoluciones.", "F7", false),
            new("reports", "Reportes", "Revisa ventas, caja y utilidades.", "F8", false),
            new("settings", "Configuración", "Negocio, impresora, backup y moneda.", "F9", false)
        };

        Metrics = new ObservableCollection<DashboardMetric>
        {
            new("Total temporal", "$0", "Se conectará al caso de uso de venta."),
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
}
