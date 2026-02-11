namespace LoTo.Domain.Entities;

public class GameSession
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public Guid? WinnerPlayerId { get; set; }
    public int? WinnerRow { get; set; }
    public int TotalNumbersDrawn { get; set; }

    // Navigation
    public Room? Room { get; set; }
    public RoomPlayer? WinnerPlayer { get; set; }
}
