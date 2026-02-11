namespace LoTo.Application.Interfaces;

public record TokenPair(string Token, string RefreshToken);

public interface IJwtService
{
    TokenPair GenerateTokens(Guid userId, string email, string role);
    Guid? ValidateRefreshToken(string refreshToken);
}
