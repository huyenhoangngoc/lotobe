using System.Security.Claims;
using LoTo.Domain.Entities;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoTo.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IUserRepository _userRepo;
    private readonly ISystemSettingRepository _settingRepo;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        ITransactionRepository transactionRepo,
        IUserRepository userRepo,
        ISystemSettingRepository settingRepo,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _transactionRepo = transactionRepo;
        _userRepo = userRepo;
        _settingRepo = settingRepo;
        _logger = logger;
    }

    /// <summary>
    /// Check global premium status (public)
    /// </summary>
    [HttpGet("premium-status")]
    [ProducesResponseType(typeof(PremiumStatusResponse), 200)]
    public async Task<IActionResult> GetPremiumStatus(CancellationToken ct)
    {
        var setting = await _settingRepo.GetByKeyAsync("global_premium_enabled", ct);
        var enabled = setting?.Value == "true";
        return Ok(new PremiumStatusResponse(enabled));
    }

    /// <summary>
    /// Tao Stripe Checkout Session
    /// </summary>
    [HttpPost("create")]
    [Authorize]
    [ProducesResponseType(typeof(CreatePaymentResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        if (request.PlanType is not "yearly")
            return BadRequest(new { error = "INVALID_PLAN", message = "Plan type phai la yearly" });

        try
        {
            var result = await _paymentService.CreateCheckoutSessionAsync(userId, request.PlanType, ct);

            // Luu transaction
            var transaction = new Transaction
            {
                UserId = userId,
                StripeSessionId = result.SessionId,
                Amount = result.Amount,
                PlanType = request.PlanType,
                Status = TransactionStatus.Pending,
            };
            await _transactionRepo.CreateAsync(transaction, ct);

            return Ok(new CreatePaymentResponse(
                result.SessionId, result.CheckoutUrl, result.Amount, result.ExpireAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Stripe checkout session for user {UserId}", userId);
            return BadRequest(new { error = "PAYMENT_FAILED", message = ex.Message });
        }
    }

    /// <summary>
    /// Stripe webhook (server-to-server)
    /// </summary>
    [HttpPost("stripe-webhook")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        var payload = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? "";

        _logger.LogInformation("Stripe webhook received");

        var success = await _paymentService.HandleWebhookAsync(payload, signature, ct);
        if (!success)
        {
            _logger.LogWarning("Stripe webhook handling failed");
            return BadRequest();
        }

        return Ok();
    }

    /// <summary>
    /// Check trang thai payment
    /// </summary>
    [HttpGet("status/{sessionId}")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentStatusResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPaymentStatus(string sessionId, CancellationToken ct)
    {
        var transaction = await _transactionRepo.GetByStripeSessionIdAsync(sessionId, ct);
        if (transaction is null)
            return NotFound(new { error = "NOT_FOUND" });

        return Ok(new PaymentStatusResponse(
            transaction.StripeSessionId ?? "",
            transaction.Status.ToString().ToLower(),
            transaction.Amount,
            transaction.PlanType));
    }

    /// <summary>
    /// Verify payment qua Stripe API
    /// </summary>
    [HttpPost("verify/{sessionId}")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentStatusResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> VerifyPayment(string sessionId, CancellationToken ct)
    {
        var transaction = await _transactionRepo.GetByStripeSessionIdAsync(sessionId, ct);
        if (transaction is null)
            return NotFound(new { error = "NOT_FOUND" });

        // Da xu ly roi
        if (transaction.Status == TransactionStatus.Completed)
            return Ok(new PaymentStatusResponse(sessionId, "completed", transaction.Amount, transaction.PlanType));

        // Goi Stripe API
        try
        {
            var result = await _paymentService.QuerySessionAsync(sessionId, ct);

            if (result.IsCompleted)
            {
                // Thanh toan thanh cong - upgrade premium
                transaction.Status = TransactionStatus.Completed;
                transaction.StripePaymentIntentId = result.PaymentIntentId;
                transaction.CompletedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateAsync(transaction, ct);

                var user = await _userRepo.GetByIdAsync(transaction.UserId, ct);
                if (user is not null)
                {
                    user.IsPremium = true;
                    user.PremiumExpiresAt = DateTime.UtcNow.AddYears(1);
                    await _userRepo.UpdateAsync(user, ct);
                    _logger.LogInformation("User {UserId} upgraded to premium via verify", user.Id);
                }

                return Ok(new PaymentStatusResponse(sessionId, "completed", transaction.Amount, transaction.PlanType));
            }

            // Chua thanh toan
            return Ok(new PaymentStatusResponse(sessionId, "pending", transaction.Amount, transaction.PlanType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify payment {SessionId}", sessionId);
            return BadRequest(new { error = "VERIFY_FAILED", message = ex.Message });
        }
    }
}

public record CreatePaymentRequest(string PlanType);
public record CreatePaymentResponse(string SessionId, string CheckoutUrl, long Amount, DateTime ExpireAt);
public record PaymentStatusResponse(string SessionId, string Status, long Amount, string PlanType);
public record PremiumStatusResponse(bool GlobalPremiumEnabled);
