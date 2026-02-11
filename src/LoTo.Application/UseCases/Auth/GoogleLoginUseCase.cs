using LoTo.Application.DTOs;
using LoTo.Application.Interfaces;
using LoTo.Domain.Entities;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace LoTo.Application.UseCases.Auth;

public class GoogleLoginUseCase
{
    private readonly IGoogleAuthService _googleAuth;
    private readonly IJwtService _jwtService;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<GoogleLoginUseCase> _logger;

    public GoogleLoginUseCase(
        IGoogleAuthService googleAuth,
        IJwtService jwtService,
        IUserRepository userRepo,
        ILogger<GoogleLoginUseCase> logger)
    {
        _googleAuth = googleAuth;
        _jwtService = jwtService;
        _userRepo = userRepo;
        _logger = logger;
    }

    public async Task<AuthResponse> ExecuteAsync(string idToken, CancellationToken ct = default)
    {
        var googleUser = await _googleAuth.VerifyIdTokenAsync(idToken, ct);

        var user = await _userRepo.GetByGoogleIdAsync(googleUser.GoogleId, ct);

        if (user is null)
        {
            user = new User
            {
                GoogleId = googleUser.GoogleId,
                Email = googleUser.Email,
                DisplayName = googleUser.DisplayName,
                AvatarUrl = googleUser.AvatarUrl,
                Role = UserRole.Host,
            };
            user = await _userRepo.CreateAsync(user, ct);
            _logger.LogInformation("New user created: {Email}", user.Email);
        }
        else
        {
            user.DisplayName = googleUser.DisplayName;
            user.AvatarUrl = googleUser.AvatarUrl;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user, ct);
        }

        if (user.IsBanned)
            throw new InvalidOperationException("Tai khoan da bi khoa");

        var tokens = _jwtService.GenerateTokens(user.Id, user.Email!, user.Role.ToString().ToLower());

        return new AuthResponse(tokens.Token, tokens.RefreshToken, new UserDto(
            user.Id,
            user.DisplayName,
            user.Email!,
            user.AvatarUrl,
            user.Role.ToString().ToLower(),
            user.IsPremium,
            user.PremiumExpiresAt
        ));
    }
}
