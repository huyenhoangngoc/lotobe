using LoTo.Application.DTOs;
using LoTo.Application.Interfaces;
using LoTo.Application.UseCases.Auth;
using LoTo.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoTo.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly GoogleLoginUseCase _googleLoginUseCase;
    private readonly IJwtService _jwtService;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        GoogleLoginUseCase googleLoginUseCase,
        IJwtService jwtService,
        IUserRepository userRepo,
        ILogger<AuthController> logger)
    {
        _googleLoginUseCase = googleLoginUseCase;
        _jwtService = jwtService;
        _userRepo = userRepo;
        _logger = logger;
    }

    /// <summary>
    /// Google OAuth login/register
    /// </summary>
    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _googleLoginUseCase.ExecuteAsync(request.IdToken, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("khoa"))
        {
            return StatusCode(403, new { error = "FORBIDDEN", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google login failed");
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Dang nhap that bai" });
        }
    }

    /// <summary>
    /// Refresh JWT token
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var userId = _jwtService.ValidateRefreshToken(request.RefreshToken);
        if (userId is null)
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Token het han" });

        var user = await _userRepo.GetByIdAsync(userId.Value, ct);
        if (user is null || user.IsBanned)
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Tai khoan khong hop le" });

        var tokens = _jwtService.GenerateTokens(user.Id, user.Email!, user.Role.ToString().ToLower());
        return Ok(new { token = tokens.Token, refreshToken = tokens.RefreshToken });
    }
}
