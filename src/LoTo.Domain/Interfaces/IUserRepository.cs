namespace LoTo.Domain.Interfaces;

using LoTo.Domain.Entities;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User> CreateAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<(List<User> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? search = null, CancellationToken ct = default);
}
