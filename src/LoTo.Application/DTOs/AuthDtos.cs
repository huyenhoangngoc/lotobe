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
    bool IsBanned = false,
    DateTime? TermsAcceptedAt = null,
    string? TermsVersion = null
);

public record AcceptTermsRequest(string TermsVersion);

public record AcceptTermsResponse(DateTime AcceptedAt, string TermsVersion);
