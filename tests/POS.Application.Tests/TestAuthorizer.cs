using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Application.Services;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Infrastructure.Persistence;

namespace POS.Application.Tests;

internal static class TestAuthorizer
{
    /// <summary>
    /// dbContext is optional because most callers use in-memory fakes; when a real AppDbContext
    /// is passed, the actor is persisted so FK-enforced writes (Sales/Returns -> Users) succeed.
    /// </summary>
    public static CriticalCommandAuthorizer Create(Guid? operatorId = null, AppDbContext? dbContext = null)
    {
        var user = new User("test-operator", "hash", RoleConstants.AdministratorRoleId);
        if (operatorId.HasValue)
        {
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, operatorId.Value);
        }
        if (dbContext is not null)
        {
            SeedAsync(dbContext, user).GetAwaiter().GetResult();
        }
        return new CriticalCommandAuthorizer(new Session(user.Id), new Users(user), new Roles());
    }

    private static async Task SeedAsync(AppDbContext dbContext, User user)
    {
        if (!await dbContext.Roles.AnyAsync(r => r.Id == RoleConstants.AdministratorRoleId))
        {
            dbContext.Roles.Add(Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName));
        }
        if (!await dbContext.Users.AnyAsync(u => u.Id == user.Id))
        {
            // Username is per-Id so multiple distinct actors can be seeded into the same dbContext
            // without tripping the Users.Username unique index.
            var persisted = new User($"test-operator-{user.Id:N}", "hash", RoleConstants.AdministratorRoleId);
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(persisted, user.Id);
            dbContext.Users.Add(persisted);
        }
        await dbContext.SaveChangesAsync();
    }

    private sealed class Session(Guid operatorId) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = operatorId;
        public string Username => "test-operator";
        public bool IsAuthenticated => true;
    }

    private sealed class Users(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task AddAsync(User value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>([user]);
        public Task UpdateAsync(User value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class Roles : IRoleRepository
    {
        private static readonly Role Administrator = Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName);
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(id == Administrator.Id ? Administrator : null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([Administrator]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
