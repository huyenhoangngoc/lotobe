namespace LoTo.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid PlayerId { get; set; }
    public string Grid { get; set; } = string.Empty; // JSONB: {"rows": [[...],[...],[...]]}
    public int[] MarkedNumbers { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public GameSession? GameSession { get; set; }
    public RoomPlayer? Player { get; set; }
}
