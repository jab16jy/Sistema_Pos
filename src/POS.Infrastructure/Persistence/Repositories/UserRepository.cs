using Microsoft.EntityFrameworkCore;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<bool> AnyAsync(CancellationToken ct = default)
        => _dbContext.Users.AnyAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        if (!await _dbContext.Roles.AnyAsync(ct))
        {
            _dbContext.Roles.AddRange(
                Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName),
                Role.CreateSystemRole(RoleConstants.CashierRoleId, RoleConstants.CashierRoleName));
        }

        _dbContext.Users.Add(user);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
        => await _dbContext.Users.OrderBy(u => u.Username).ToListAsync(ct);

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _dbContext.Users.Update(user);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(User user, CancellationToken ct = default)
    {
        _dbContext.Users.Remove(user);
        return Task.CompletedTask;
    }

    public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default)
        => _dbContext.Users.CountAsync(user => user.RoleId == roleId && user.IsActive, ct);
}
