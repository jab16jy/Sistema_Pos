using Microsoft.EntityFrameworkCore;
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

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _dbContext.Users.Add(user);
        return Task.CompletedTask;
    }
}
