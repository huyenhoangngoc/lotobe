namespace LoTo.Domain.Entities;

using LoTo.Domain.Enums;

public class Room
{
    public Guid Id { get; set; }
    public Guid HostId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public int MaxPlayers { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? Host { get; set; }
}
