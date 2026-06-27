using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _dbContext;

    public SettingsRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.BusinessSettings.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(BusinessSettings settings, CancellationToken cancellationToken = default)
    {
        bool exists = await _dbContext.BusinessSettings
            .AnyAsync(current => current.Id == settings.Id, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            _dbContext.BusinessSettings.Update(settings);
            return;
        }

        await _dbContext.BusinessSettings.AddAsync(settings, cancellationToken).ConfigureAwait(false);
    }
}
