namespace LoTo.Domain.Interfaces;

public interface IPaymentService
{
    Task<PaymentResult> CreatePaymentAsync(Guid userId, string planType, CancellationToken ct = default);
    Task<bool> VerifyCallbackAsync(string signature, string rawBody, CancellationToken ct = default);
    Task<QueryPaymentResult> QueryPaymentAsync(string orderId, CancellationToken ct = default);
}

public record PaymentResult(string OrderId, string PayUrl, string? QrCodeUrl, long Amount, DateTime ExpireAt);
public record QueryPaymentResult(int ResultCode, string Message, string? TransId);
