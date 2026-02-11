namespace LoTo.Domain.Entities;

public class RoomPlayer
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
    public bool IsConnected { get; set; } = true;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Room? Room { get; set; }
}
