namespace LoTo.Domain.Interfaces;

using LoTo.Domain.Entities;

public interface IDrawnNumberRepository
{
    Task<DrawnNumber> CreateAsync(DrawnNumber drawnNumber, CancellationToken ct = default);
    Task<List<DrawnNumber>> GetBySessionIdAsync(Guid gameSessionId, CancellationToken ct = default);
    Task<int> CountBySessionIdAsync(Guid gameSessionId, CancellationToken ct = default);
}
