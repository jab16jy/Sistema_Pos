using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Domain.Enums;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class UserManagementViewModel : ViewModelBase
{
    private readonly GetAllUsersUseCase _getAllUsers;
    private readonly CreateUserUseCase _createUser;
    private readonly UpdateUserUseCase _updateUser;
    private readonly DeactivateUserUseCase _deactivateUser;
    private readonly ICurrentOperatorSession _session;

    private UserSummary? _selectedUser;
    private string _newUsername = string.Empty;
    private string _newPassword = string.Empty;
    private UserRole _newRole = UserRole.Cashier;
    private string _editPassword = string.Empty;
    private UserRole _editRole = UserRole.Cashier;
    private string _statusMessage = string.Empty;
    private bool _hasError;
    private bool _isLoading;

    public UserManagementViewModel(
        GetAllUsersUseCase getAllUsers,
        CreateUserUseCase createUser,
        UpdateUserUseCase updateUser,
        DeactivateUserUseCase deactivateUser,
        ICurrentOperatorSession session)
    {
        _getAllUsers = getAllUsers;
        _createUser = createUser;
        _updateUser = updateUser;
        _deactivateUser = deactivateUser;
        _session = session;

        LoadUsersCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => SetUnexpectedError($"Error al cargar usuarios: {ex.Message}")));
        CreateUserCommand = new RelayCommand(_ => RunSafe(CreateAsync(), ex => SetUnexpectedError($"Error al crear usuario: {ex.Message}")), _ => IsAdmin);
        SaveEditCommand = new RelayCommand(_ => RunSafe(SaveEditAsync(), ex => SetUnexpectedError($"Error al actualizar usuario: {ex.Message}")), _ => IsAdmin && SelectedUser is not null);
        DeactivateCommand = new RelayCommand(_ => RunSafe(DeactivateAsync(), ex => SetUnexpectedError($"Error al desactivar usuario: {ex.Message}")), _ => IsAdmin && SelectedUser is not null);
    }

    public ObservableCollection<UserSummary> Users { get; } = new();

    public IReadOnlyList<UserRole> AvailableRoles { get; } = [UserRole.Cashier, UserRole.Administrator];

    public UserSummary? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value) && value is not null)
            {
                EditPassword = string.Empty;
                EditRole = value.Role;
                ((RelayCommand)SaveEditCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeactivateCommand).RaiseCanExecuteChanged();
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

    public UserRole NewRole
    {
        get => _newRole;
        set => SetProperty(ref _newRole, value);
    }

    public string EditPassword
    {
        get => _editPassword;
        set => SetProperty(ref _editPassword, value);
    }

    public UserRole EditRole
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

    public bool IsAdmin => _session.IsAuthenticated && _session.Role == UserRole.Administrator;

    public ICommand LoadUsersCommand { get; }
    public ICommand CreateUserCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand DeactivateCommand { get; }

    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        HasError = false;
        try
        {
            var list = await _getAllUsers.ExecuteAsync();
            Users.Clear();
            foreach (var u in list)
                Users.Add(u);
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

    // ponytail: UI-level trust boundary — this is a single-process desktop app, so the ViewModel
    // is the operation boundary. Move the check into the use case if a non-UI caller ever appears.
    private bool EnsureAdmin()
    {
        if (_session.Role == UserRole.Administrator)
        {
            return true;
        }

        HasError = true;
        StatusMessage = "Solo un administrador puede gestionar usuarios.";
        return false;
    }

    private async Task CreateAsync()
    {
        if (!EnsureAdmin()) return;

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
            NewRole = UserRole.Cashier;
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
        if (!EnsureAdmin()) return;
        if (SelectedUser is null) return;
        if (!EnsureAdmin()) return;

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
        if (!EnsureAdmin()) return;
        if (SelectedUser is null) return;
        if (!EnsureAdmin()) return;

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
            StatusMessage = "Usuario desactivado.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            SetUnexpectedError($"Error al desactivar usuario: {ex.Message}");
        }
    }

    private void SetUnexpectedError(string message)
    {
        HasError = true;
        StatusMessage = message;
    }

    private bool EnsureAdmin()
    {
        if (IsAdmin)
        {
            return true;
        }

        HasError = true;
        StatusMessage = "Solo un administrador puede gestionar usuarios y permisos.";
        return false;
    }
}
