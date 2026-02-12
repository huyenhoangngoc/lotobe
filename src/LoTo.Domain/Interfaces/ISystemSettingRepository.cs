namespace LoTo.Domain.Interfaces;

using LoTo.Domain.Entities;

public interface ISystemSettingRepository
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task UpsertAsync(string key, string value, Guid? updatedBy, CancellationToken ct = default);
}
