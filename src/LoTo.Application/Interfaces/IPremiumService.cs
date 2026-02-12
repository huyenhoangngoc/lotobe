namespace LoTo.Application.Interfaces;

public interface IPremiumService
{
    Task<bool> IsGlobalPremiumEnabledAsync(CancellationToken ct = default);
    Task<bool> IsUserPremiumAsync(Guid userId, CancellationToken ct = default);
}
