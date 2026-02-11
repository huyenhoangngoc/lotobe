using Dapper;
using LoTo.Domain.Entities;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LoTo.Infrastructure.Persistence.Repositories;

public class DrawnNumberRepository : IDrawnNumberRepository
{
    private readonly string _connectionString;

    public DrawnNumberRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Supabase connection string not configured");
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<DrawnNumber> CreateAsync(DrawnNumber drawnNumber, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var id = await conn.QuerySingleAsync<Guid>("""
            INSERT INTO drawn_numbers (game_session_id, number, drawn_order, drawn_at)
            VALUES (@GameSessionId, @Number, @DrawnOrder, @DrawnAt)
            RETURNING id
            """,
            new
            {
                drawnNumber.GameSessionId,
                drawnNumber.Number,
                drawnNumber.DrawnOrder,
                drawnNumber.DrawnAt,
            });
        drawnNumber.Id = id;
        return drawnNumber;
    }

    public async Task<List<DrawnNumber>> GetBySessionIdAsync(Guid gameSessionId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<DrawnNumberRow>(
            "SELECT * FROM drawn_numbers WHERE game_session_id = @GameSessionId ORDER BY drawn_order",
            new { GameSessionId = gameSessionId });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<int> CountBySessionIdAsync(Guid gameSessionId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM drawn_numbers WHERE game_session_id = @GameSessionId",
            new { GameSessionId = gameSessionId });
    }

    private class DrawnNumberRow
    {
        public Guid Id { get; set; }
        public Guid Game_Session_Id { get; set; }
        public int Number { get; set; }
        public int Drawn_Order { get; set; }
        public DateTime Drawn_At { get; set; }

        public DrawnNumber ToEntity() => new()
        {
            Id = Id,
            GameSessionId = Game_Session_Id,
            Number = Number,
            DrawnOrder = Drawn_Order,
            DrawnAt = Drawn_At,
        };
    }
}
