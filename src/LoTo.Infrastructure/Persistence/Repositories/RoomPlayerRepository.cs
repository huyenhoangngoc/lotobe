using Dapper;
using LoTo.Domain.Entities;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LoTo.Infrastructure.Persistence.Repositories;

public class RoomPlayerRepository : IRoomPlayerRepository
{
    private readonly string _connectionString;

    public RoomPlayerRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Supabase connection string not configured");
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<RoomPlayer?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<PlayerRow>(
            "SELECT * FROM room_players WHERE id = @Id", new { Id = id });
        return row?.ToEntity();
    }

    public async Task<RoomPlayer> CreateAsync(RoomPlayer player, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var id = await conn.QuerySingleAsync<Guid>("""
            INSERT INTO room_players (room_id, nickname, connection_id, is_connected)
            VALUES (@RoomId, @Nickname, @ConnectionId, @IsConnected)
            RETURNING id
            """,
            new
            {
                player.RoomId,
                player.Nickname,
                player.ConnectionId,
                player.IsConnected,
            });
        player.Id = id;
        return player;
    }

    public async Task UpdateAsync(RoomPlayer player, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE room_players SET
                connection_id = @ConnectionId,
                is_connected = @IsConnected
            WHERE id = @Id
            """,
            new { player.Id, player.ConnectionId, player.IsConnected });
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM room_players WHERE id = @Id", new { Id = id });
    }

    public async Task<List<RoomPlayer>> GetByRoomIdAsync(Guid roomId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<PlayerRow>(
            "SELECT * FROM room_players WHERE room_id = @RoomId ORDER BY joined_at",
            new { RoomId = roomId });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<int> CountByRoomIdAsync(Guid roomId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM room_players WHERE room_id = @RoomId",
            new { RoomId = roomId });
    }

    public async Task<bool> NicknameExistsInRoomAsync(Guid roomId, string nickname, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM room_players WHERE room_id = @RoomId AND nickname = @Nickname)",
            new { RoomId = roomId, Nickname = nickname });
    }

    private class PlayerRow
    {
        public Guid Id { get; set; }
        public Guid Room_Id { get; set; }
        public string Nickname { get; set; } = "";
        public string? Connection_Id { get; set; }
        public bool Is_Connected { get; set; }
        public DateTime Joined_At { get; set; }

        public RoomPlayer ToEntity() => new()
        {
            Id = Id,
            RoomId = Room_Id,
            Nickname = Nickname,
            ConnectionId = Connection_Id,
            IsConnected = Is_Connected,
            JoinedAt = Joined_At,
        };
    }
}
