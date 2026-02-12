namespace LoTo.Domain.Interfaces;

public interface IPaymentService
{
    Task<PaymentResult> CreateCheckoutSessionAsync(Guid userId, string planType, CancellationToken ct = default);
    Task<bool> HandleWebhookAsync(string payload, string signature, CancellationToken ct = default);
    Task<QueryPaymentResult> QuerySessionAsync(string sessionId, CancellationToken ct = default);
}

public record PaymentResult(string SessionId, string CheckoutUrl, long Amount, DateTime ExpireAt);
public record QueryPaymentResult(bool IsCompleted, string? PaymentIntentId, string? ErrorMessage);
