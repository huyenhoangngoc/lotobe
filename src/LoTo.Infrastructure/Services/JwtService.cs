using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LoTo.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LoTo.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly int _accessTokenMinutes;
    private readonly int _refreshTokenDays;

    public JwtService(IConfiguration config)
    {
        _secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret not configured");
        _issuer = config["Jwt:Issuer"] ?? "LoToOnline";
        _accessTokenMinutes = int.TryParse(config["Jwt:AccessTokenMinutes"], out var m) ? m : 60;
        _refreshTokenDays = int.TryParse(config["Jwt:RefreshTokenDays"], out var d) ? d : 7;
    }

    public TokenPair GenerateTokens(Guid userId, string email, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenMinutes),
            signingCredentials: creds
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Refresh token: encode userId + random bytes + expiry
        var refreshToken = GenerateRefreshToken(userId);

        return new TokenPair(accessToken, refreshToken);
    }

    public Guid? ValidateRefreshToken(string refreshToken)
    {
        try
        {
            var bytes = Convert.FromBase64String(refreshToken);
            // Format: 16 bytes userId + 8 bytes expiry + 32 bytes random
            if (bytes.Length != 56) return null;

            var userId = new Guid(bytes[..16]);
            var expiryTicks = BitConverter.ToInt64(bytes, 16);
            var expiry = new DateTimeOffset(expiryTicks, TimeSpan.Zero);

            if (expiry < DateTimeOffset.UtcNow) return null;

            return userId;
        }
        catch
        {
            return null;
        }
    }

    private string GenerateRefreshToken(Guid userId)
    {
        var bytes = new byte[56];
        userId.ToByteArray().CopyTo(bytes, 0);
        BitConverter.GetBytes(DateTimeOffset.UtcNow.AddDays(_refreshTokenDays).UtcTicks).CopyTo(bytes, 16);
        RandomNumberGenerator.Fill(bytes.AsSpan(24));
        return Convert.ToBase64String(bytes);
    }
}
