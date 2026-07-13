using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _dbContext;
    public RoleRepository(AppDbContext dbContext) => _dbContext = dbContext;
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => _dbContext.Roles.FirstOrDefaultAsync(role => role.Id == id, ct);
    public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => _dbContext.Roles.FirstOrDefaultAsync(role => role.Name == name, ct);
    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => await _dbContext.Roles.OrderBy(role => role.Name).ToListAsync(ct);
    public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => _dbContext.RolePermissions.AnyAsync(permission => permission.RoleId == roleId && permission.PermissionKey == permissionKey, ct);
    public async Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => (await _dbContext.RolePermissions.Where(permission => permission.RoleId == roleId).Select(permission => permission.PermissionKey).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
    public Task AddAsync(Role role, CancellationToken ct = default) { _dbContext.Roles.Add(role); return Task.CompletedTask; }
    public Task UpdateAsync(Role role, CancellationToken ct = default) { _dbContext.Roles.Update(role); return Task.CompletedTask; }
    public async Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default)
    {
        await _dbContext.RolePermissions.Where(permission => permission.RoleId == roleId).ExecuteDeleteAsync(ct);
        await _dbContext.RolePermissions.AddRangeAsync(keys.Select(key => new RolePermission(roleId, key)), ct);
    }
    public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => _dbContext.Users.CountAsync(user => user.RoleId == roleId, ct);
}
