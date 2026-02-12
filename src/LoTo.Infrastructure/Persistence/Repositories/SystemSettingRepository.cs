using Dapper;
using LoTo.Domain.Entities;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LoTo.Infrastructure.Persistence.Repositories;

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly string _connectionString;

    public SystemSettingRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Supabase connection string not configured");
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<SystemSettingRow>(
            "SELECT * FROM system_settings WHERE key = @Key", new { Key = key });
        return row?.ToEntity();
    }

    public async Task UpsertAsync(string key, string value, Guid? updatedBy, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO system_settings (key, value, updated_at, updated_by)
            VALUES (@Key, @Value, now(), @UpdatedBy)
            ON CONFLICT (key) DO UPDATE SET
                value = @Value,
                updated_at = now(),
                updated_by = @UpdatedBy
            """,
            new { Key = key, Value = value, UpdatedBy = updatedBy });
    }

    private class SystemSettingRow
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public DateTime Updated_At { get; set; }
        public Guid? Updated_By { get; set; }

        public SystemSetting ToEntity() => new()
        {
            Key = Key,
            Value = Value,
            UpdatedAt = Updated_At,
            UpdatedBy = Updated_By,
        };
    }
}
