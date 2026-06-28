using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure;

public static class DatabaseSchemaRollout
{
    public static async Task EnsureLatestSchemaAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        await SeedDefaultAdminAsync(scope.ServiceProvider, dbContext, cancellationToken);
    }

    private static async Task SeedDefaultAdminAsync(IServiceProvider sp, AppDbContext dbContext, CancellationToken ct)
    {
        if (await dbContext.Users.AnyAsync(ct))
            return;

        var hasher = sp.GetRequiredService<IPasswordHashService>();
        var admin = new User("admin", hasher.HashPassword("admin123"), UserRole.Administrator);
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(ct);
    }
}
