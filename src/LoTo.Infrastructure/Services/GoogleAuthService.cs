using Google.Apis.Auth;
using LoTo.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LoTo.Infrastructure.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly string _clientId;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(IConfiguration config, ILogger<GoogleAuthService> logger)
    {
        _clientId = config["Google:ClientId"]
            ?? throw new InvalidOperationException("Google:ClientId not configured");
        _logger = logger;
    }

    public async Task<GoogleUserInfo> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [_clientId]
        };

        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

        _logger.LogInformation("Google token verified for {Email}", payload.Email);

        return new GoogleUserInfo(
            payload.Subject,
            payload.Email,
            payload.Name,
            payload.Picture
        );
    }
}
