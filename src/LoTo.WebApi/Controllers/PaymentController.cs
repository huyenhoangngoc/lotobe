using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        ITransactionRepository transactionRepo,
        IUserRepository userRepo,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _transactionRepo = transactionRepo;
        _userRepo = userRepo;
        _logger = logger;
    }

    /// <summary>
    /// Tạo MoMo payment
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
            var result = await _paymentService.CreatePaymentAsync(userId, request.PlanType, ct);

            // Lưu transaction
            var transaction = new Transaction
            {
                UserId = userId,
                MomoOrderId = result.OrderId,
                Amount = result.Amount,
                PlanType = request.PlanType,
                Status = TransactionStatus.Pending,
            };
            await _transactionRepo.CreateAsync(transaction, ct);

            return Ok(new CreatePaymentResponse(
                result.OrderId, result.PayUrl, result.QrCodeUrl, result.Amount, result.ExpireAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MoMo payment for user {UserId}", userId);
            return BadRequest(new { error = "PAYMENT_FAILED", message = ex.Message });
        }
    }

    /// <summary>
    /// MoMo IPN callback (server-to-server)
    /// </summary>
    [HttpPost("momo-ipn")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> MoMoIpn(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);

        _logger.LogInformation("MoMo IPN received: {Body}", body);

        try
        {
            var data = JsonSerializer.Deserialize<JsonElement>(body);
            var orderId = data.GetProperty("orderId").GetString()!;
            var resultCode = data.GetProperty("resultCode").GetInt32();
            var signature = data.GetProperty("signature").GetString()!;
            var transId = data.TryGetProperty("transId", out var tid) ? tid.GetString() : null;

            // Verify signature
            var accessKey = data.TryGetProperty("accessKey", out var ak) ? ak.GetString() ?? "" : "";
            var amount = data.GetProperty("amount").GetInt64();
            var extraData = data.TryGetProperty("extraData", out var ed) ? ed.GetString() ?? "" : "";
            var message = data.GetProperty("message").GetString() ?? "";
            var orderInfo = data.TryGetProperty("orderInfo", out var oi) ? oi.GetString() ?? "" : "";
            var orderType = data.TryGetProperty("orderType", out var ot) ? ot.GetString() ?? "" : "";
            var partnerCode = data.GetProperty("partnerCode").GetString() ?? "";
            var requestId = data.GetProperty("requestId").GetString() ?? "";
            var responseTime = data.TryGetProperty("responseTime", out var rt) ? rt.GetInt64().ToString() : "";

            var rawSignature = $"accessKey={accessKey}&amount={amount}&extraData={extraData}" +
                $"&message={message}&orderId={orderId}&orderInfo={orderInfo}" +
                $"&orderType={orderType}&partnerCode={partnerCode}&payType=&requestId={requestId}" +
                $"&responseTime={responseTime}&resultCode={resultCode}&transId={transId}";

            var verified = await _paymentService.VerifyCallbackAsync(signature, rawSignature, ct);
            if (!verified)
            {
                _logger.LogWarning("MoMo IPN signature verification failed for order {OrderId}", orderId);
                return NoContent();
            }

            // Tìm transaction
            var transaction = await _transactionRepo.GetByMomoOrderIdAsync(orderId, ct);
            if (transaction is null)
            {
                _logger.LogWarning("MoMo IPN: transaction not found for order {OrderId}", orderId);
                return NoContent();
            }

            if (resultCode == 0)
            {
                // Thanh toán thành công
                transaction.Status = TransactionStatus.Completed;
                transaction.MomoTransId = transId;
                transaction.MomoResponse = body;
                transaction.CompletedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateAsync(transaction, ct);

                // Upgrade premium
                var user = await _userRepo.GetByIdAsync(transaction.UserId, ct);
                if (user is not null)
                {
                    user.IsPremium = true;
                    user.PremiumExpiresAt = DateTime.UtcNow.AddYears(1);
                    await _userRepo.UpdateAsync(user, ct);
                    _logger.LogInformation("User {UserId} upgraded to premium (yearly)", user.Id);
                }
            }
            else
            {
                transaction.Status = TransactionStatus.Failed;
                transaction.MomoResponse = body;
                await _transactionRepo.UpdateAsync(transaction, ct);
                _logger.LogInformation("MoMo payment failed for order {OrderId}, code: {Code}", orderId, resultCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo IPN");
        }

        return NoContent();
    }

    /// <summary>
    /// Check trạng thái payment
    /// </summary>
    [HttpGet("status/{orderId}")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentStatusResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPaymentStatus(string orderId, CancellationToken ct)
    {
        var transaction = await _transactionRepo.GetByMomoOrderIdAsync(orderId, ct);
        if (transaction is null)
            return NotFound(new { error = "NOT_FOUND" });

        return Ok(new PaymentStatusResponse(
            transaction.MomoOrderId ?? "",
            transaction.Status.ToString().ToLower(),
            transaction.Amount,
            transaction.PlanType));
    }

    /// <summary>
    /// Verify payment qua MoMo Query API - dùng khi IPN không đến được (localhost)
    /// </summary>
    [HttpPost("verify/{orderId}")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentStatusResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> VerifyPayment(string orderId, CancellationToken ct)
    {
        var transaction = await _transactionRepo.GetByMomoOrderIdAsync(orderId, ct);
        if (transaction is null)
            return NotFound(new { error = "NOT_FOUND" });

        // Đã xử lý rồi
        if (transaction.Status == TransactionStatus.Completed)
            return Ok(new PaymentStatusResponse(orderId, "completed", transaction.Amount, transaction.PlanType));

        // Gọi MoMo Query API
        try
        {
            var result = await _paymentService.QueryPaymentAsync(orderId, ct);

            if (result.ResultCode == 0)
            {
                // Thanh toán thành công - upgrade premium
                transaction.Status = TransactionStatus.Completed;
                transaction.MomoTransId = result.TransId;
                transaction.CompletedAt = DateTime.UtcNow;
                await _transactionRepo.UpdateAsync(transaction, ct);

                var user = await _userRepo.GetByIdAsync(transaction.UserId, ct);
                if (user is not null)
                {
                    user.IsPremium = true;
                    user.PremiumExpiresAt = transaction.PlanType == "yearly"
                        ? DateTime.UtcNow.AddYears(1)
                        : DateTime.UtcNow.AddMonths(1);
                    await _userRepo.UpdateAsync(user, ct);
                    _logger.LogInformation("User {UserId} upgraded to premium via verify ({Plan})", user.Id, transaction.PlanType);
                }

                return Ok(new PaymentStatusResponse(orderId, "completed", transaction.Amount, transaction.PlanType));
            }

            if (result.ResultCode == 1000)
            {
                // Đang chờ thanh toán
                return Ok(new PaymentStatusResponse(orderId, "pending", transaction.Amount, transaction.PlanType));
            }

            // Thất bại
            transaction.Status = TransactionStatus.Failed;
            await _transactionRepo.UpdateAsync(transaction, ct);
            return Ok(new PaymentStatusResponse(orderId, "failed", transaction.Amount, transaction.PlanType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify payment {OrderId}", orderId);
            return BadRequest(new { error = "VERIFY_FAILED", message = ex.Message });
        }
    }
}

public record CreatePaymentRequest(string PlanType);
public record CreatePaymentResponse(string OrderId, string PayUrl, string? QrCodeUrl, long Amount, DateTime ExpireAt);
public record PaymentStatusResponse(string OrderId, string Status, long Amount, string PlanType);
