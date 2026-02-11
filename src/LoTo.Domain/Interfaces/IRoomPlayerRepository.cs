namespace LoTo.Domain.Interfaces;

using LoTo.Domain.Entities;

public interface IRoomPlayerRepository
{
    Task<RoomPlayer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RoomPlayer> CreateAsync(RoomPlayer player, CancellationToken ct = default);
    Task UpdateAsync(RoomPlayer player, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<List<RoomPlayer>> GetByRoomIdAsync(Guid roomId, CancellationToken ct = default);
    Task<int> CountByRoomIdAsync(Guid roomId, CancellationToken ct = default);
    Task<bool> NicknameExistsInRoomAsync(Guid roomId, string nickname, CancellationToken ct = default);
}
