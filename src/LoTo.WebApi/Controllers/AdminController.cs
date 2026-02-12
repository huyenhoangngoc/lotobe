using System.Security.Claims;
using LoTo.Application.DTOs;
using LoTo.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoTo.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly ITransactionRepository _transactionRepo;
    private readonly ISystemSettingRepository _settingRepo;

    public AdminController(
        IUserRepository userRepo,
        ITransactionRepository transactionRepo,
        ISystemSettingRepository settingRepo)
    {
        _userRepo = userRepo;
        _transactionRepo = transactionRepo;
        _settingRepo = settingRepo;
    }

    /// <summary>
    /// Danh sach users (Admin only)
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(PagedResult<UserDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (items, totalCount) = await _userRepo.GetAllAsync(page, pageSize, search, ct);

        var dtos = items.Select(u => new UserDto(
            u.Id,
            u.DisplayName,
            u.Email ?? "",
            u.AvatarUrl,
            u.Role.ToString().ToLower(),
            u.IsPremium,
            u.PremiumExpiresAt,
            u.IsBanned
        )).ToList();

        return Ok(new PagedResult<UserDto>(dtos, totalCount, page, pageSize));
    }

    /// <summary>
    /// Ban user (Admin only)
    /// </summary>
    [HttpPost("users/{userId}/ban")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> BanUser(Guid userId, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null) return NotFound(new { error = "USER_NOT_FOUND" });

        // Khong ban chinh minh
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (adminId == userId.ToString())
            return BadRequest(new { error = "CANNOT_BAN_SELF" });

        user.IsBanned = true;
        await _userRepo.UpdateAsync(user, ct);
        return Ok(new { message = "User da bi ban" });
    }

    /// <summary>
    /// Unban user (Admin only)
    /// </summary>
    [HttpPost("users/{userId}/unban")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UnbanUser(Guid userId, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null) return NotFound(new { error = "USER_NOT_FOUND" });

        user.IsBanned = false;
        await _userRepo.UpdateAsync(user, ct);
        return Ok(new { message = "User da duoc unban" });
    }

    /// <summary>
    /// Bao cao doanh thu (Admin only)
    /// </summary>
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(RevenueResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var (items, totalCount) = await _transactionRepo.GetAllAsync(page, pageSize, ct);

        var completed = items.Where(t => t.Status == Domain.Enums.TransactionStatus.Completed).ToList();
        var totalRevenue = completed.Sum(t => t.Amount);

        var dtos = items.Select(t => new TransactionDto(
            t.Id,
            t.UserId,
            t.StripeSessionId ?? "",
            t.Amount,
            t.PlanType,
            t.Status.ToString().ToLower(),
            t.CreatedAt,
            t.CompletedAt
        )).ToList();

        return Ok(new RevenueResponse(totalRevenue, totalCount, dtos, page, pageSize));
    }

    /// <summary>
    /// Lay trang thai Global Premium (Admin only)
    /// </summary>
    [HttpGet("settings/global-premium")]
    [ProducesResponseType(typeof(GlobalPremiumResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetGlobalPremium(CancellationToken ct)
    {
        var setting = await _settingRepo.GetByKeyAsync("global_premium_enabled", ct);
        var enabled = setting?.Value == "true";
        return Ok(new GlobalPremiumResponse(enabled));
    }

    /// <summary>
    /// Bat/tat Global Premium (Admin only)
    /// </summary>
    [HttpPost("settings/global-premium")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> SetGlobalPremium([FromBody] SetGlobalPremiumRequest request, CancellationToken ct)
    {
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        Guid? adminId = Guid.TryParse(adminIdStr, out var id) ? id : null;

        await _settingRepo.UpsertAsync("global_premium_enabled", request.Enabled ? "true" : "false", adminId, ct);
        return Ok(new { message = request.Enabled ? "Global Premium da bat" : "Global Premium da tat" });
    }
}

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
public record TransactionDto(Guid Id, Guid UserId, string SessionId, long Amount, string PlanType, string Status, DateTime CreatedAt, DateTime? CompletedAt);
public record RevenueResponse(long TotalRevenue, int TotalTransactions, List<TransactionDto> Items, int Page, int PageSize);
public record GlobalPremiumResponse(bool Enabled);
public record SetGlobalPremiumRequest(bool Enabled);
