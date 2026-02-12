namespace LoTo.Domain.Entities;

using LoTo.Domain.Enums;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? StripeSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public long Amount { get; set; }
    public string PlanType { get; set; } = "yearly";
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? StripeResponse { get; set; } // JSONB
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
