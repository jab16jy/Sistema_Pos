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
        return GetOrDefaultAsync(cancellationToken);
    }

    private async Task<BusinessSettings?> GetOrDefaultAsync(CancellationToken cancellationToken)
    {
        BusinessSettings? persisted = await _dbContext.BusinessSettings.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return persisted ?? new BusinessSettings(
            CompanyDefaults.BusinessName,
            CompanyDefaults.BusinessAddress,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            "COP");
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
