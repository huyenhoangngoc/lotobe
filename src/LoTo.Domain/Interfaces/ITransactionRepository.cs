namespace LoTo.Domain.Interfaces;

using LoTo.Domain.Entities;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Transaction?> GetByMomoOrderIdAsync(string momoOrderId, CancellationToken ct = default);
    Task<Transaction> CreateAsync(Transaction transaction, CancellationToken ct = default);
    Task UpdateAsync(Transaction transaction, CancellationToken ct = default);
    Task<List<Transaction>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<(List<Transaction> Items, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
}
