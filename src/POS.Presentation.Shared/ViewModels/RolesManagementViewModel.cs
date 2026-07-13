using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using POS.Application.Interfaces;
using POS.Application.UseCases.Roles;
using POS.Domain.Authorization;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class PermissionOptionRow : ViewModelBase
{
    private bool _isGranted;
    private bool _isEnabled = true;

    public PermissionOptionRow(string key, string module, bool isGranted)
    {
        Key = key;
        Module = module;
        _isGranted = isGranted;
        DisplayName = GetPermissionDisplayName(key);
        Description = GetPermissionDescription(key);
    }

    public string Key { get; }
    public string Module { get; }
    public string DisplayName { get; }
    public string Description { get; }

    public bool IsGranted
    {
        get => _isGranted;
        set => SetProperty(ref _isGranted, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        private set => SetProperty(ref _isEnabled, value);
    }

    public void SetEnabled(bool enabled) => IsEnabled = enabled;

    private static string GetPermissionDisplayName(string key)
    {
        return key switch
        {
            "Users.View" => "Ver usuarios",
            "Users.Create" => "Crear usuarios",
            "Users.Edit" => "Editar usuarios",
            "Users.Deactivate" => "Desactivar usuarios",
            "Roles.View" => "Ver roles",
            "Roles.Create" => "Crear roles",
            "Roles.Edit" => "Editar roles",
            "Roles.Deactivate" => "Desactivar roles",
            "Roles.AssignPermissions" => "Asignar permisos",
            "Audit.View" => "Ver bitácora de auditoría",
            "Settings.View" => "Ver configuración",
            "Settings.Edit" => "Editar configuración",
            "Admin.Backup.View" => "Ver copias de seguridad",
            "Admin.Backup.Create" => "Crear copias de seguridad",
            "Admin.Backup.Restore" => "Restaurar copias de seguridad",
            "Invoices.View" => "Ver facturas",
            "Invoices.Reprint" => "Reimprimir facturas",
            "Invoices.Return" => "Gestionar devoluciones",
            "Invoices.Cancel" => "Anular facturas",
            "Cashier.Sell" => "Realizar ventas",
            "Products.View" => "Ver catálogo de productos",
            "Products.Create" => "Crear productos",
            "Products.Edit" => "Editar productos",
            "Products.Delete" => "Eliminar productos",
            "Products.Import" => "Importar productos masivamente",
            "Categories.View" => "Ver categorías",
            "Categories.Create" => "Crear categorías",
            "Categories.Edit" => "Editar categorías",
            "Categories.Delete" => "Eliminar categorías",
            "Inventory.View" => "Ver stock e inventario",
            "Inventory.Adjust" => "Ajustar stock e inventario",
            "Quotes.View" => "Ver cotizaciones",
            "Quotes.Create" => "Crear cotizaciones",
            "Quotes.Convert" => "Convertir cotizaciones a venta",
            "Suppliers.View" => "Ver proveedores",
            "Suppliers.Create" => "Crear proveedores",
            "Suppliers.Edit" => "Editar proveedores",
            "Suppliers.Delete" => "Eliminar proveedores",
            "Purchases.View" => "Ver compras a proveedores",
            "Purchases.Create" => "Registrar compras de mercancía",
            "Customers.View" => "Ver clientes",
            "Customers.Create" => "Registrar nuevos clientes",
            "Customers.Edit" => "Editar clientes",
            "Receivables.View" => "Ver cuentas por cobrar",
            "Receivables.ApplyPayment" => "Registrar abonos y pagos",
            "Reports.View" => "Ver reportes y estadísticas",
            "CashRegister.Close" => "Realizar arqueo y cierre de caja",
            "Shifts.View" => "Ver turnos de trabajo",
            "Shifts.Open" => "Abrir turnos de trabajo",
            "Shifts.Close" => "Cerrar turnos de trabajo",
            "Loyalty.View" => "Ver puntos de fidelidad",
            "Loyalty.Redeem" => "Canjear puntos de fidelidad",
            "Loyalty.ConfigureRules" => "Configurar reglas de fidelidad",
            "Loyalty.ManageTiers" => "Administrar niveles de fidelidad",
            "Barcode.View" => "Ver códigos de barras",
            "Barcode.Print" => "Imprimir códigos de barras",
            "Documents.Send" => "Enviar comprobantes electrónicos",
            _ => key
        };
    }

    private static string GetPermissionDescription(string key)
    {
        return key switch
        {
            "Users.View" => "Permite ver la lista de usuarios del sistema y sus detalles básicos.",
            "Users.Create" => "Permite registrar nuevos usuarios en el sistema.",
            "Users.Edit" => "Permite modificar los datos y el rol de los usuarios existentes.",
            "Users.Deactivate" => "Permite desactivar o suspender temporalmente a un usuario.",
            "Roles.View" => "Permite consultar los roles definidos y sus permisos asociados.",
            "Roles.Create" => "Permite crear nuevos roles de usuario en el sistema.",
            "Roles.Edit" => "Permite modificar el nombre de los roles existentes.",
            "Roles.Deactivate" => "Permite desactivar roles creados (excepto los del sistema).",
            "Roles.AssignPermissions" => "Permite asignar o revocar permisos específicos a los roles.",
            "Audit.View" => "Permite visualizar el registro de auditoría y bitácora del sistema.",
            "Settings.View" => "Permite ver la configuración general del sistema.",
            "Settings.Edit" => "Permite modificar los parámetros y configuraciones globales.",
            "Admin.Backup.View" => "Permite ver el historial de copias de seguridad de la base de datos.",
            "Admin.Backup.Create" => "Permite realizar una nueva copia de seguridad del sistema.",
            "Admin.Backup.Restore" => "Permite restaurar el sistema a partir de una copia de seguridad.",
            "Invoices.View" => "Permite buscar y consultar facturas de venta emitidas.",
            "Invoices.Reprint" => "Permite reimprimir copias físicas o digitales de las facturas.",
            "Invoices.Return" => "Permite gestionar devoluciones de productos facturados.",
            "Invoices.Cancel" => "Permite anular facturas registradas en el sistema.",
            "Cashier.Sell" => "Permite abrir el módulo de ventas y realizar cobros en caja.",
            "Products.View" => "Permite visualizar el catálogo y detalles de los productos.",
            "Products.Create" => "Permite agregar nuevos productos al inventario.",
            "Products.Edit" => "Permite modificar la información, precios y detalles de productos.",
            "Products.Delete" => "Permite eliminar de forma permanente productos del catálogo.",
            "Products.Import" => "Permite importar productos de forma masiva (ej. Excel/CSV).",
            "Categories.View" => "Permite consultar las categorías de productos configuradas.",
            "Categories.Create" => "Permite crear nuevas categorías para organizar los productos.",
            "Categories.Edit" => "Permite editar el nombre y propiedades de las categorías.",
            "Categories.Delete" => "Permite eliminar categorías de productos existentes.",
            "Inventory.View" => "Permite ver existencias, stock actual y Kardex de inventario.",
            "Inventory.Adjust" => "Permite realizar ajustes manuales de entrada o salida al stock.",
            "Quotes.View" => "Permite ver el listado de cotizaciones y presupuestos.",
            "Quotes.Create" => "Permite elaborar y registrar nuevas cotizaciones.",
            "Quotes.Convert" => "Permite convertir una cotización en una venta o factura directa.",
            "Suppliers.View" => "Permite consultar la lista de proveedores registrados.",
            "Suppliers.Create" => "Permite registrar nuevos proveedores.",
            "Suppliers.Edit" => "Permite modificar los datos de contacto y comerciales de proveedores.",
            "Suppliers.Delete" => "Permite dar de baja o eliminar proveedores de la base de datos.",
            "Purchases.View" => "Permite consultar las órdenes y facturas de compras a proveedores.",
            "Purchases.Create" => "Permite registrar nuevas compras de mercancía recibida.",
            "Customers.View" => "Permite consultar el listado y cuentas de clientes.",
            "Customers.Create" => "Permite registrar nuevos clientes en el sistema.",
            "Customers.Edit" => "Permite modificar la información general y límites de crédito de clientes.",
            "Receivables.View" => "Permite ver el estado de cuentas por cobrar y saldos pendientes.",
            "Receivables.ApplyPayment" => "Permite registrar abonos y pagos de clientes a sus deudas.",
            "Reports.View" => "Permite visualizar reportes estadísticos y resúmenes financieros.",
            "CashRegister.Close" => "Permite realizar el cierre diario de caja y arqueo de dinero.",
            "Shifts.View" => "Permite monitorear los turnos de trabajo de los cajeros.",
            "Shifts.Open" => "Permite iniciar o abrir un nuevo turno de trabajo.",
            "Shifts.Close" => "Permite finalizar o cerrar el turno de trabajo activo.",
            "Loyalty.View" => "Permite consultar el estado de puntos y clientes fidelizados.",
            "Loyalty.Redeem" => "Permite canjear puntos acumulados por beneficios o descuentos.",
            "Loyalty.ConfigureRules" => "Permite ajustar las reglas de acumulación de puntos por compras.",
            "Loyalty.ManageTiers" => "Permite administrar niveles de fidelización de clientes.",
            "Barcode.View" => "Permite ver las etiquetas y códigos de barras de los productos.",
            "Barcode.Print" => "Permite mandar a imprimir etiquetas de códigos de barras.",
            "Documents.Send" => "Permite enviar facturas o documentos tributarios por correo/API.",
            _ => "Permiso del sistema."
        };
    }
}

public sealed class RolesManagementViewModel : ViewModelBase
{
    private readonly GetAllRolesUseCase _getAllRoles;
    private readonly CreateRoleUseCase _createRole;
    private readonly UpdateRoleUseCase _updateRole;
    private readonly DeactivateRoleUseCase _deactivateRole;
    private readonly IRoleRepository _roles;
    private readonly ICurrentOperatorSession _session;
    private readonly IReadOnlySet<string> _emptyGrantSet = new HashSet<string>(StringComparer.Ordinal);

    private RoleSummary? _selectedRole;
    private IReadOnlySet<string> _loadedGrants = new HashSet<string>(StringComparer.Ordinal);
    private string _roleName = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _hasError;
    private bool _isLoading;

    public RolesManagementViewModel(
        GetAllRolesUseCase getAllRoles,
        CreateRoleUseCase createRole,
        UpdateRoleUseCase updateRole,
        DeactivateRoleUseCase deactivateRole,
        IRoleRepository roles,
        ICurrentOperatorSession session)
    {
        _getAllRoles = getAllRoles;
        _createRole = createRole;
        _updateRole = updateRole;
        _deactivateRole = deactivateRole;
        _roles = roles;
        _session = session;

        foreach (string key in PermissionKeys.All)
        {
            var option = new PermissionOptionRow(key, GetModuleName(key), false);
            option.PropertyChanged += PermissionOptionChanged;
            PermissionOptions.Add(option);
        }

        LoadRolesCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => SetUnexpectedError($"Error al cargar roles: {ex.Message}")), _ => CanLoad);
        CreateRoleCommand = new RelayCommand(_ => RunSafe(CreateAsync(), ex => SetUnexpectedError($"Error al crear rol: {ex.Message}")), _ => CanCreate);
        SaveRoleCommand = new RelayCommand(_ => RunSafe(SaveAsync(), ex => SetUnexpectedError($"Error al guardar rol: {ex.Message}")), _ => CanSave);
        DeactivateRoleCommand = new RelayCommand(_ => RunSafe(DeactivateAsync(), ex => SetUnexpectedError($"Error al desactivar rol: {ex.Message}")), _ => CanDeactivate);
    }

    public ObservableCollection<RoleSummary> Roles { get; } = new();
    public ObservableCollection<PermissionOptionRow> PermissionOptions { get; } = new();

    public RoleSummary? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (!SetProperty(ref _selectedRole, value))
            {
                return;
            }

            RoleName = value?.Name ?? string.Empty;
            RunSafe(LoadSelectedRoleGrantsAsync(), ex => SetUnexpectedError($"Error al cargar permisos del rol: {ex.Message}"));
            RaiseActionCanExecuteChanged();
        }
    }

    public string RoleName
    {
        get => _roleName;
        set
        {
            if (SetProperty(ref _roleName, value ?? string.Empty))
            {
                RaiseActionCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool CanLoad => _session.HasPermission(PermissionKeys.Roles.View);
    public bool CanCreate => _session.HasPermission(PermissionKeys.Roles.Create);
    public bool CanDeactivate => _session.HasPermission(PermissionKeys.Roles.Deactivate) && SelectedRole is { IsSystem: false };
    public bool HasPermissionChanges => !CurrentGrantSet().SetEquals(_loadedGrants);
    public bool CanSave => SelectedRole is not null
        && _session.HasPermission(PermissionKeys.Roles.Edit)
        && (!HasPermissionChanges || _session.HasPermission(PermissionKeys.Roles.AssignPermissions));

    public ICommand LoadRolesCommand { get; }
    public ICommand CreateRoleCommand { get; }
    public ICommand SaveRoleCommand { get; }
    public ICommand DeactivateRoleCommand { get; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!EnsurePermission(CanLoad, "No tienes permiso para consultar roles."))
        {
            return;
        }

        IsLoading = true;
        HasError = false;
        StatusMessage = string.Empty;
        try
        {
            IReadOnlyList<RoleSummary> result = await _getAllRoles.ExecuteAsync(_session.OperatorId, ct).ConfigureAwait(true);
            Roles.Clear();
            foreach (RoleSummary role in result)
            {
                Roles.Add(role);
            }
        }
        catch (Exception ex)
        {
            SetUnexpectedError($"Error al cargar roles: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSelectedRoleGrantsAsync(CancellationToken ct = default)
    {
        IReadOnlySet<string> grants = SelectedRole is null
            ? _emptyGrantSet
            : await _roles.GetPermissionKeysAsync(SelectedRole.Id, ct).ConfigureAwait(true);

        _loadedGrants = new HashSet<string>(grants, StringComparer.Ordinal);
        bool canEditGrants = SelectedRole is not { Id: var id } || !RoleConstants.IsSystemAdministrator(id);
        foreach (PermissionOptionRow option in PermissionOptions)
        {
            option.IsGranted = grants.Contains(option.Key);
            option.SetEnabled(canEditGrants);
        }

        OnPropertyChanged(nameof(HasPermissionChanges));
        RaiseActionCanExecuteChanged();
    }

    private async Task CreateAsync()
    {
        if (!EnsurePermission(CanCreate, "No tienes permiso para crear roles."))
        {
            return;
        }

        var result = await _createRole.ExecuteAsync(RoleName, CurrentGrantSet(), _session.OperatorId).ConfigureAwait(true);
        if (!result.Success)
        {
            SetUnexpectedError(result.Error ?? "No fue posible crear el rol.");
            return;
        }

        RoleName = string.Empty;
        ClearPermissionOptions();
        StatusMessage = "Rol creado correctamente.";
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task SaveAsync()
    {
        if (!EnsurePermission(CanSave, "No tienes permiso para guardar los cambios del rol.") || SelectedRole is null)
        {
            return;
        }

        var result = await _updateRole.ExecuteAsync(SelectedRole.Id, RoleName, CurrentGrantSet(), _session.OperatorId).ConfigureAwait(true);
        if (!result.Success)
        {
            SetUnexpectedError(result.Error ?? "No fue posible actualizar el rol.");
            return;
        }

        StatusMessage = "Rol actualizado correctamente.";
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task DeactivateAsync()
    {
        if (!EnsurePermission(CanDeactivate, "No tienes permiso para desactivar este rol.") || SelectedRole is null)
        {
            return;
        }

        var result = await _deactivateRole.ExecuteAsync(SelectedRole.Id, _session.OperatorId).ConfigureAwait(true);
        if (!result.Success)
        {
            SetUnexpectedError(result.Error ?? "No fue posible desactivar el rol.");
            return;
        }

        SelectedRole = null;
        StatusMessage = "Rol desactivado.";
        await LoadAsync().ConfigureAwait(true);
    }

    private void PermissionOptionChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(PermissionOptionRow.IsGranted))
        {
            OnPropertyChanged(nameof(HasPermissionChanges));
            RaiseActionCanExecuteChanged();
        }
    }

    private IReadOnlySet<string> CurrentGrantSet() => PermissionOptions
        .Where(option => option.IsGranted)
        .Select(option => option.Key)
        .ToHashSet(StringComparer.Ordinal);

    private void ClearPermissionOptions()
    {
        _loadedGrants = _emptyGrantSet;
        foreach (PermissionOptionRow option in PermissionOptions)
        {
            option.IsGranted = false;
            option.SetEnabled(true);
        }
    }

    private bool EnsurePermission(bool allowed, string message)
    {
        if (allowed)
        {
            return true;
        }

        SetUnexpectedError(message);
        return false;
    }

    private void RaiseActionCanExecuteChanged()
    {
        ((RelayCommand)CreateRoleCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SaveRoleCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DeactivateRoleCommand).RaiseCanExecuteChanged();
    }

    private void SetUnexpectedError(string message)
    {
        HasError = true;
        StatusMessage = message;
    }

    private static string GetModuleName(string key) => key[..key.LastIndexOf('.')];
}
