namespace LoTo.Domain.Entities;

public class DrawnNumber
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public int Number { get; set; }
    public int DrawnOrder { get; set; }
    public DateTime DrawnAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public GameSession? GameSession { get; set; }
}
