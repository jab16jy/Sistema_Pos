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
        => _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);

    public Task<bool> AnyAsync(CancellationToken ct = default)
        => _dbContext.Users.AnyAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        if (!await _dbContext.Roles.AnyAsync(ct))
        {
            _dbContext.Roles.AddRange(
                Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName),
                Role.CreateSystemRole(RoleConstants.CashierRoleId, RoleConstants.CashierRoleName));
            await _dbContext.SaveChangesAsync(ct);
        }

        // CreateUserUseCase has no unit of work, so persist here (mirrors UpdateAsync below).
        // Without this the new user only lived in the change tracker and was never saved.
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
        => await _dbContext.Users.OrderBy(u => u.Username).ToListAsync(ct);

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(ct);
    }

    public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default)
        => _dbContext.Users.CountAsync(user => user.RoleId == roleId && user.IsActive, ct);
}
