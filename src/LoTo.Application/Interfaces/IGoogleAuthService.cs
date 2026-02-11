namespace LoTo.Application.Interfaces;

public record GoogleUserInfo(string GoogleId, string Email, string DisplayName, string? AvatarUrl);

public interface IGoogleAuthService
{
    Task<GoogleUserInfo> VerifyIdTokenAsync(string idToken, CancellationToken ct = default);
}
