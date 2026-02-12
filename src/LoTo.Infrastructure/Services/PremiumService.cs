using LoTo.Application.Interfaces;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace LoTo.Infrastructure.Services;

public class PremiumService : IPremiumService
{
    private readonly ISystemSettingRepository _settingRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<PremiumService> _logger;

    public PremiumService(
        ISystemSettingRepository settingRepo,
        IUserRepository userRepo,
        ILogger<PremiumService> logger)
    {
        _settingRepo = settingRepo;
        _userRepo = userRepo;
        _logger = logger;
    }

    public async Task<bool> IsGlobalPremiumEnabledAsync(CancellationToken ct = default)
    {
        var setting = await _settingRepo.GetByKeyAsync("global_premium_enabled", ct);
        return setting?.Value == "true";
    }

    public async Task<bool> IsUserPremiumAsync(Guid userId, CancellationToken ct = default)
    {
        // Check global premium first
        if (await IsGlobalPremiumEnabledAsync(ct))
            return true;

        // Check individual premium
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null) return false;

        return user.IsPremium && (user.PremiumExpiresAt == null || user.PremiumExpiresAt > DateTime.UtcNow);
    }
}
