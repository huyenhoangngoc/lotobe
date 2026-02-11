using LoTo.Application.DTOs;
using LoTo.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LoTo.WebApi.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepo;

    public UsersController(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null)
            return NotFound(new { error = "NOT_FOUND", message = "User khong ton tai" });

        return Ok(new UserDto(
            user.Id,
            user.DisplayName,
            user.Email!,
            user.AvatarUrl,
            user.Role.ToString().ToLower(),
            user.IsPremium,
            user.PremiumExpiresAt,
            user.IsBanned,
            user.TermsAcceptedAt,
            user.TermsVersion
        ));
    }

    /// <summary>
    /// Accept terms of service
    /// </summary>
    [HttpPost("me/accept-terms")]
    [Authorize]
    [ProducesResponseType(typeof(AcceptTermsResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> AcceptTerms([FromBody] AcceptTermsRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null)
            return NotFound(new { error = "NOT_FOUND", message = "User khong ton tai" });

        user.TermsAcceptedAt = DateTime.UtcNow;
        user.TermsVersion = request.TermsVersion;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);

        return Ok(new AcceptTermsResponse(user.TermsAcceptedAt.Value, user.TermsVersion));
    }
}
