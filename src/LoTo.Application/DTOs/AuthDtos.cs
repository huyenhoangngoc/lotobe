namespace LoTo.Application.DTOs;

public record GoogleLoginRequest(string IdToken);

public record RefreshTokenRequest(string RefreshToken);

public record AuthResponse(string Token, string RefreshToken, UserDto User);

public record UserDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    string Role,
    bool IsPremium,
    DateTime? PremiumExpiresAt,
    bool IsBanned = false
);
