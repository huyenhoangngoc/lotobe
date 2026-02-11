namespace LoTo.Domain.Entities;

using LoTo.Domain.Enums;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? MomoOrderId { get; set; }
    public string? MomoTransId { get; set; }
    public long Amount { get; set; }
    public string PlanType { get; set; } = "monthly"; // monthly, yearly
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? MomoResponse { get; set; } // JSONB
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
