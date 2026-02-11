namespace LoTo.Domain.Interfaces;

public interface IAppSettingsRepository
{
    Task<bool> IsGlobalPremiumEnabledAsync(CancellationToken ct = default);
    Task SetGlobalPremiumEnabledAsync(bool enabled, CancellationToken ct = default);
}
