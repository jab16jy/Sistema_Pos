using POS.Application.Interfaces;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class CreateUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashService _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRoleRepository _roles;

    public CreateUserUseCase(IUserRepository users, IRoleRepository roles, IPasswordHashService hasher, IUnitOfWork unitOfWork)
    {
        _users = users;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _roles = roles;
    }

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        string username, string password, Guid roleId, Guid requestingUserId, CancellationToken ct = default)
    {
        var authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
            _users,
            _roles,
            requestingUserId,
            PermissionKeys.Users.Create,
            "No tienes permiso para crear usuarios.",
            ct).ConfigureAwait(false);
        if (authorizationError is not null)
            return (false, authorizationError);

        if (roleId == RoleConstants.AdministratorRoleId)
        {
            var requester = await _users.GetByIdAsync(requestingUserId, ct).ConfigureAwait(false);
            if (requester is null || !RoleConstants.IsSystemAdministrator(requester.RoleId))
            {
                return (false, "No tienes permiso para asignar el rol de Administrador.");
            }
        }

        var roleAssignmentAuthorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
            _users,
            _roles,
            requestingUserId,
            PermissionKeys.Roles.AssignPermissions,
            "No tienes permiso para asignar roles.",
            ct).ConfigureAwait(false);
        if (roleAssignmentAuthorizationError is not null)
            return (false, roleAssignmentAuthorizationError);

        if (string.IsNullOrWhiteSpace(username))
            return (false, "El nombre de usuario es requerido.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            return (false, "La contraseña debe tener al menos 4 caracteres.");

        var existing = await _users.GetByUsernameAsync(username.Trim(), ct);
        if (existing is not null)
            return (false, "Ya existe un usuario con ese nombre.");

        var hash = _hasher.HashPassword(password);
        var user = new User(username.Trim(), hash, roleId);
        await _users.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return (true, null);
    }
}
