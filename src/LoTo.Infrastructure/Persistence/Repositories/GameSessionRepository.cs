using Dapper;
using LoTo.Domain.Entities;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LoTo.Infrastructure.Persistence.Repositories;

public class GameSessionRepository : IGameSessionRepository
{
    private readonly string _connectionString;

    public GameSessionRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Supabase connection string not configured");
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<GameSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<SessionRow>(
            "SELECT * FROM game_sessions WHERE id = @Id", new { Id = id });
        return row?.ToEntity();
    }

    public async Task<GameSession?> GetActiveByRoomIdAsync(Guid roomId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<SessionRow>(
            "SELECT * FROM game_sessions WHERE room_id = @RoomId AND ended_at IS NULL ORDER BY started_at DESC LIMIT 1",
            new { RoomId = roomId });
        return row?.ToEntity();
    }

    public async Task<GameSession> CreateAsync(GameSession session, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var id = await conn.QuerySingleAsync<Guid>("""
            INSERT INTO game_sessions (room_id, started_at, total_numbers_drawn)
            VALUES (@RoomId, @StartedAt, @TotalNumbersDrawn)
            RETURNING id
            """,
            new
            {
                session.RoomId,
                session.StartedAt,
                session.TotalNumbersDrawn,
            });
        session.Id = id;
        return session;
    }

    public async Task UpdateAsync(GameSession session, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE game_sessions SET
                ended_at = @EndedAt,
                winner_player_id = @WinnerPlayerId,
                winner_row = @WinnerRow,
                total_numbers_drawn = @TotalNumbersDrawn
            WHERE id = @Id
            """,
            new
            {
                session.Id,
                session.EndedAt,
                session.WinnerPlayerId,
                session.WinnerRow,
                session.TotalNumbersDrawn,
            });
    }

    private class SessionRow
    {
        public Guid Id { get; set; }
        public Guid Room_Id { get; set; }
        public DateTime Started_At { get; set; }
        public DateTime? Ended_At { get; set; }
        public Guid? Winner_Player_Id { get; set; }
        public int? Winner_Row { get; set; }
        public int Total_Numbers_Drawn { get; set; }

        public GameSession ToEntity() => new()
        {
            Id = Id,
            RoomId = Room_Id,
            StartedAt = Started_At,
            EndedAt = Ended_At,
            WinnerPlayerId = Winner_Player_Id,
            WinnerRow = Winner_Row,
            TotalNumbersDrawn = Total_Numbers_Drawn,
        };
    }
}
