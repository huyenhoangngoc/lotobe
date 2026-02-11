namespace LoTo.Domain.Interfaces;

using LoTo.Domain.Entities;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Ticket> CreateAsync(Ticket ticket, CancellationToken ct = default);
    Task UpdateAsync(Ticket ticket, CancellationToken ct = default);
    Task<Ticket?> GetBySessionAndPlayerAsync(Guid gameSessionId, Guid playerId, CancellationToken ct = default);
    Task<List<Ticket>> GetBySessionIdAsync(Guid gameSessionId, CancellationToken ct = default);
}
