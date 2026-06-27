using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ISettingsRepository
{
    Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(BusinessSettings settings, CancellationToken cancellationToken = default);
}
