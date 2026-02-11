using Dapper;
using LoTo.Domain.Entities;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LoTo.Infrastructure.Persistence.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly string _connectionString;

    public TicketRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Supabase connection string not configured");
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<TicketRow>(
            "SELECT * FROM tickets WHERE id = @Id", new { Id = id });
        return row?.ToEntity();
    }

    public async Task<Ticket> CreateAsync(Ticket ticket, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var id = await conn.QuerySingleAsync<Guid>("""
            INSERT INTO tickets (game_session_id, player_id, grid, marked_numbers)
            VALUES (@GameSessionId, @PlayerId, @Grid::jsonb, @MarkedNumbers)
            RETURNING id
            """,
            new
            {
                ticket.GameSessionId,
                ticket.PlayerId,
                ticket.Grid,
                MarkedNumbers = ticket.MarkedNumbers,
            });
        ticket.Id = id;
        return ticket;
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE tickets SET
                marked_numbers = @MarkedNumbers
            WHERE id = @Id
            """,
            new { ticket.Id, MarkedNumbers = ticket.MarkedNumbers });
    }

    public async Task<Ticket?> GetBySessionAndPlayerAsync(Guid gameSessionId, Guid playerId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<TicketRow>(
            "SELECT * FROM tickets WHERE game_session_id = @GameSessionId AND player_id = @PlayerId",
            new { GameSessionId = gameSessionId, PlayerId = playerId });
        return row?.ToEntity();
    }

    public async Task<List<Ticket>> GetBySessionIdAsync(Guid gameSessionId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<TicketRow>(
            "SELECT * FROM tickets WHERE game_session_id = @GameSessionId",
            new { GameSessionId = gameSessionId });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    private class TicketRow
    {
        public Guid Id { get; set; }
        public Guid Game_Session_Id { get; set; }
        public Guid Player_Id { get; set; }
        public string Grid { get; set; } = "";
        public int[] Marked_Numbers { get; set; } = [];
        public DateTime Created_At { get; set; }

        public Ticket ToEntity() => new()
        {
            Id = Id,
            GameSessionId = Game_Session_Id,
            PlayerId = Player_Id,
            Grid = Grid,
            MarkedNumbers = Marked_Numbers,
            CreatedAt = Created_At,
        };
    }
}
