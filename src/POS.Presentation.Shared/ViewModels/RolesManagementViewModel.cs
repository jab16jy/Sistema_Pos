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
    }

    public string Key { get; }
    public string Module { get; }

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
