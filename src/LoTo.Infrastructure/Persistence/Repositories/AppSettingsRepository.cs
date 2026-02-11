using Dapper;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LoTo.Infrastructure.Persistence.Repositories;

public class AppSettingsRepository : IAppSettingsRepository
{
    private readonly string _connectionString;

    public AppSettingsRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Supabase connection string not configured");
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<bool> IsGlobalPremiumEnabledAsync(CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var value = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT value FROM app_settings WHERE key = 'global_premium_enabled'");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetGlobalPremiumEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"INSERT INTO app_settings (key, value, updated_at) VALUES ('global_premium_enabled', @Value, now())
              ON CONFLICT (key) DO UPDATE SET value = @Value, updated_at = now()",
            new { Value = enabled ? "true" : "false" });
    }
}
