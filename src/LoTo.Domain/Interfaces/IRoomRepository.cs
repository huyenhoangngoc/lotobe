namespace LoTo.Domain.Interfaces;

using LoTo.Domain.Entities;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Room?> GetByCodeAsync(string roomCode, CancellationToken ct = default);
    Task<Room> CreateAsync(Room room, CancellationToken ct = default);
    Task UpdateAsync(Room room, CancellationToken ct = default);
    Task<List<Room>> GetActiveByHostIdAsync(Guid hostId, CancellationToken ct = default);
    Task<Dictionary<Guid, (int TotalRooms, bool HasActiveRoom)>> GetRoomStatsByHostIdsAsync(
        IEnumerable<Guid> hostIds, CancellationToken ct = default);
}
