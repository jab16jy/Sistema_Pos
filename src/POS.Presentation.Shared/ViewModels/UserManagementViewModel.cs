using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Application.UseCases.Roles;
using POS.Domain.Authorization;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class UserManagementViewModel : ViewModelBase
{
    private readonly GetAllUsersUseCase _getAllUsers;
    private readonly CreateUserUseCase _createUser;
    private readonly UpdateUserUseCase _updateUser;
    private readonly DeactivateUserUseCase _deactivateUser;
    private readonly ActivateUserUseCase _activateUser;
    private readonly DeleteUserUseCase _deleteUser;
    private readonly GetAllRolesUseCase _getAllRoles;
    private readonly ICurrentOperatorSession _session;

    private UserSummary? _selectedUser;
    private string _newUsername = string.Empty;
    private string _newPassword = string.Empty;
    private Guid _newRole = RoleConstants.CashierRoleId;
    private string _editPassword = string.Empty;
    private Guid _editRole = RoleConstants.CashierRoleId;
    private string _statusMessage = string.Empty;
    private bool _hasError;
    private bool _isLoading;

    public UserManagementViewModel(
        GetAllUsersUseCase getAllUsers,
        CreateUserUseCase createUser,
        UpdateUserUseCase updateUser,
        DeactivateUserUseCase deactivateUser,
        ActivateUserUseCase activateUser,
        DeleteUserUseCase deleteUser,
        GetAllRolesUseCase getAllRoles,
        ICurrentOperatorSession session)
    {
        _getAllUsers = getAllUsers;
        _createUser = createUser;
        _updateUser = updateUser;
        _deactivateUser = deactivateUser;
        _activateUser = activateUser;
        _deleteUser = deleteUser;
        _getAllRoles = getAllRoles;
        _session = session;

        LoadUsersCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => SetUnexpectedError($"Error al cargar usuarios: {ex.Message}")));
        CreateUserCommand = new RelayCommand(_ => RunSafe(CreateAsync(), ex => SetUnexpectedError($"Error al crear usuario: {ex.Message}")), _ => CanCreate);
        SaveEditCommand = new RelayCommand(_ => RunSafe(SaveEditAsync(), ex => SetUnexpectedError($"Error al actualizar usuario: {ex.Message}")), _ => CanEdit && SelectedUser is not null);
        DeactivateCommand = new RelayCommand(_ => RunSafe(DeactivateAsync(), ex => SetUnexpectedError($"Error al desactivar usuario: {ex.Message}")), _ => CanDeactivate && SelectedUser is not null && SelectedUser.IsActive);
        ActivateCommand = new RelayCommand(_ => RunSafe(ActivateAsync(), ex => SetUnexpectedError($"Error al activar usuario: {ex.Message}")), _ => CanActivate && SelectedUser is not null && !SelectedUser.IsActive);
        DeleteCommand = new RelayCommand(_ => RunSafe(DeleteAsync(), ex => SetUnexpectedError($"Error al eliminar usuario: {ex.Message}")), _ => CanDelete && SelectedUser is not null);
    }

    // Overload for backward compatibility with existing tests
    public UserManagementViewModel(
        GetAllUsersUseCase getAllUsers,
        CreateUserUseCase createUser,
        UpdateUserUseCase updateUser,
        DeactivateUserUseCase deactivateUser,
        GetAllRolesUseCase getAllRoles,
        ICurrentOperatorSession session)
        : this(getAllUsers, createUser, updateUser, deactivateUser, null!, null!, getAllRoles, session)
    {
    }

    public Func<string, string, Task<bool>>? ConfirmDeletionAsync { get; set; }

    public ObservableCollection<UserSummary> Users { get; } = new();

    public ObservableCollection<RoleSummary> AvailableRoles { get; } = new();

    public UserSummary? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value) && value is not null)
            {
                EditPassword = string.Empty;
                EditRole = value.RoleId;
                ((RelayCommand)SaveEditCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeactivateCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ActivateCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string NewUsername
    {
        get => _newUsername;
        set => SetProperty(ref _newUsername, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public Guid NewRole
    {
        get => _newRole;
        set => SetProperty(ref _newRole, value);
    }

    public string EditPassword
    {
        get => _editPassword;
        set => SetProperty(ref _editPassword, value);
    }

    public Guid EditRole
    {
        get => _editRole;
        set => SetProperty(ref _editRole, value);
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

    public bool CanCreate => _session.HasPermission(PermissionKeys.Users.Create);
    public bool CanEdit => _session.HasPermission(PermissionKeys.Users.Edit);
    public bool CanDeactivate => _session.HasPermission(PermissionKeys.Users.Deactivate);
    public bool CanActivate => _session.HasPermission(PermissionKeys.Users.Activate) || _session.HasPermission(PermissionKeys.Users.Deactivate);
    public bool CanDelete => _session.HasPermission(PermissionKeys.Users.Delete) || _session.HasPermission(PermissionKeys.Users.Deactivate);

    public ICommand LoadUsersCommand { get; }
    public ICommand CreateUserCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand DeactivateCommand { get; }
    public ICommand ActivateCommand { get; }
    public ICommand DeleteCommand { get; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        HasError = false;
        try
        {
            var list = await _getAllUsers.ExecuteAsync();
            var roles = await _getAllRoles.ExecuteAsync(_session.OperatorId);
            Users.Clear();
            foreach (var u in list)
                Users.Add(u);
            AvailableRoles.Clear();
            foreach (var role in roles.Where(role => role.IsActive))
                AvailableRoles.Add(role);

            if (AvailableRoles.All(role => role.Id != NewRole))
                NewRole = AvailableRoles.FirstOrDefault()?.Id ?? Guid.Empty;
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Error al cargar usuarios: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Mirror the use-case authorization at the UI boundary so the desktop user gets immediate feedback.
    private bool EnsurePermission(bool allowed, string message)
    {
        if (allowed)
        {
            return true;
        }

        HasError = true;
        StatusMessage = message;
        return false;
    }

    private async Task CreateAsync()
    {
        if (!EnsurePermission(CanCreate, "No tienes permiso para crear usuarios.")) return;

        try
        {
            HasError = false;
            StatusMessage = string.Empty;
            var (ok, error) = await _createUser.ExecuteAsync(NewUsername, NewPassword, NewRole, _session.OperatorId);
            if (!ok)
            {
                HasError = true;
                StatusMessage = error ?? "Error al crear usuario.";
                return;
            }
            NewUsername = string.Empty;
            NewPassword = string.Empty;
            NewRole = AvailableRoles.FirstOrDefault()?.Id ?? Guid.Empty;
            StatusMessage = "Usuario creado correctamente.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            SetUnexpectedError($"Error al crear usuario: {ex.Message}");
        }
    }

    private async Task SaveEditAsync()
    {
        if (!EnsurePermission(CanEdit, "No tienes permiso para actualizar usuarios.")) return;
        if (SelectedUser is null) return;

        try
        {
            HasError = false;
            StatusMessage = string.Empty;
            var (ok, error) = await _updateUser.ExecuteAsync(SelectedUser.Id, EditPassword, EditRole, _session.OperatorId);
            if (!ok)
            {
                HasError = true;
                StatusMessage = error ?? "Error al actualizar usuario.";
                return;
            }
            EditPassword = string.Empty;
            StatusMessage = "Usuario actualizado correctamente.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            SetUnexpectedError($"Error al actualizar usuario: {ex.Message}");
        }
    }

    private async Task DeactivateAsync()
    {
        if (!EnsurePermission(CanDeactivate, "No tienes permiso para desactivar usuarios.")) return;
        if (SelectedUser is null) return;

        try
        {
            HasError = false;
            StatusMessage = string.Empty;
            var (ok, error) = await _deactivateUser.ExecuteAsync(SelectedUser.Id, _session.OperatorId);
            if (!ok)
            {
                HasError = true;
                StatusMessage = error ?? "Error al desactivar usuario.";
                return;
            }
            SelectedUser = null;
            StatusMessage = "Usuario desactivado correctamente.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            SetUnexpectedError($"Error al desactivar usuario: {ex.Message}");
        }
    }

    private async Task ActivateAsync()
    {
        if (!EnsurePermission(CanActivate, "No tienes permiso para activar usuarios.")) return;
        if (SelectedUser is null) return;

        try
        {
            HasError = false;
            StatusMessage = string.Empty;

            if (_activateUser is null)
            {
                HasError = true;
                StatusMessage = "Servicio de activación no disponible.";
                return;
            }

            var (ok, error) = await _activateUser.ExecuteAsync(SelectedUser.Id, _session.OperatorId);
            if (!ok)
            {
                HasError = true;
                StatusMessage = error ?? "Error al activar usuario.";
                return;
            }
            SelectedUser = null;
            StatusMessage = "Usuario activado correctamente.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            SetUnexpectedError($"Error al activar usuario: {ex.Message}");
        }
    }

    private async Task DeleteAsync()
    {
        if (!EnsurePermission(CanDelete, "No tienes permiso para eliminar usuarios.")) return;
        if (SelectedUser is null) return;

        try
        {
            if (ConfirmDeletionAsync is not null)
            {
                var confirmed = await ConfirmDeletionAsync(
                    "¿Eliminar usuario?",
                    $"¿Estás seguro de que deseas eliminar permanentemente al usuario '{SelectedUser.Username}'?");
                if (!confirmed) return;
            }

            HasError = false;
            StatusMessage = string.Empty;

            if (_deleteUser is null)
            {
                HasError = true;
                StatusMessage = "Servicio de eliminación no disponible.";
                return;
            }

            var (ok, error) = await _deleteUser.ExecuteAsync(SelectedUser.Id, _session.OperatorId);
            if (!ok)
            {
                HasError = true;
                StatusMessage = error ?? "Error al eliminar usuario.";
                return;
            }
            SelectedUser = null;
            StatusMessage = "Usuario eliminado correctamente.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            SetUnexpectedError($"Error al eliminar usuario: {ex.Message}");
        }
    }

    private void SetUnexpectedError(string message)
    {
        HasError = true;
        StatusMessage = message;
    }
}
