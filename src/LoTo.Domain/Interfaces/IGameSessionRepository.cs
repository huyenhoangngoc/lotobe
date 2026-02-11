namespace LoTo.Domain.Interfaces;

using LoTo.Domain.Entities;

public interface IGameSessionRepository
{
    Task<GameSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<GameSession?> GetActiveByRoomIdAsync(Guid roomId, CancellationToken ct = default);
    Task<GameSession> CreateAsync(GameSession session, CancellationToken ct = default);
    Task UpdateAsync(GameSession session, CancellationToken ct = default);
}
