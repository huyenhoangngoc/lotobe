using Dapper;
using LoTo.Domain.Entities;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LoTo.Infrastructure.Persistence.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly string _connectionString;

    public RoomRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Supabase connection string not configured");
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<RoomRow>(
            "SELECT * FROM rooms WHERE id = @Id", new { Id = id });
        return row?.ToEntity();
    }

    public async Task<Room?> GetByCodeAsync(string roomCode, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<RoomRow>(
            "SELECT * FROM rooms WHERE room_code = @RoomCode AND status != 'finished'",
            new { RoomCode = roomCode });
        return row?.ToEntity();
    }

    public async Task<Room> CreateAsync(Room room, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var id = await conn.QuerySingleAsync<Guid>("""
            INSERT INTO rooms (host_id, room_code, status, max_players)
            VALUES (@HostId, @RoomCode, @Status, @MaxPlayers)
            RETURNING id
            """,
            new
            {
                room.HostId,
                room.RoomCode,
                Status = room.Status.ToString().ToLower(),
                room.MaxPlayers,
            });
        room.Id = id;
        return room;
    }

    public async Task UpdateAsync(Room room, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE rooms SET
                status = @Status,
                max_players = @MaxPlayers,
                closed_at = @ClosedAt,
                last_activity_at = NOW()
            WHERE id = @Id
            """,
            new
            {
                room.Id,
                Status = room.Status.ToString().ToLower(),
                room.MaxPlayers,
                room.ClosedAt,
            });
    }

    public async Task TouchActivityAsync(Guid roomId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE rooms SET last_activity_at = NOW() WHERE id = @Id",
            new { Id = roomId });
    }

    public async Task<List<Room>> GetActiveByHostIdAsync(Guid hostId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<RoomRow>(
            "SELECT * FROM rooms WHERE host_id = @HostId AND status != 'finished' ORDER BY created_at DESC",
            new { HostId = hostId });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<Dictionary<Guid, (int TotalRooms, bool HasActiveRoom)>> GetRoomStatsByHostIdsAsync(
        IEnumerable<Guid> hostIds, CancellationToken ct = default)
    {
        var ids = hostIds.ToArray();
        if (ids.Length == 0) return new();

        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<RoomStatsRow>("""
            SELECT host_id,
                COUNT(*) AS total_rooms,
                COUNT(*) FILTER (WHERE status = 'playing' AND last_activity_at > NOW() - INTERVAL '2 hours') AS active_rooms
            FROM rooms
            WHERE host_id = ANY(@HostIds)
            GROUP BY host_id
            """, new { HostIds = ids });

        return rows.ToDictionary(r => r.Host_Id, r => (r.Total_Rooms, r.Active_Rooms > 0));
    }

    private class RoomStatsRow
    {
        public Guid Host_Id { get; set; }
        public int Total_Rooms { get; set; }
        public int Active_Rooms { get; set; }
    }

    private class RoomRow
    {
        public Guid Id { get; set; }
        public Guid Host_Id { get; set; }
        public string Room_Code { get; set; } = "";
        public string Status { get; set; } = "waiting";
        public int Max_Players { get; set; }
        public DateTime Created_At { get; set; }
        public DateTime? Closed_At { get; set; }
        public DateTime Last_Activity_At { get; set; }

        public Room ToEntity() => new()
        {
            Id = Id,
            HostId = Host_Id,
            RoomCode = Room_Code,
            Status = Enum.Parse<RoomStatus>(Status, ignoreCase: true),
            MaxPlayers = Max_Players,
            CreatedAt = Created_At,
            ClosedAt = Closed_At,
            LastActivityAt = Last_Activity_At,
        };
    }
}
