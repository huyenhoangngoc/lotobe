using LoTo.Application.Interfaces;
using LoTo.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LoTo.Infrastructure.SignalR.Hubs;

public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    private readonly IConnectionMapping _connectionMapping;
    private readonly IRoomRepository _roomRepo;
    private readonly IRoomPlayerRepository _playerRepo;

    public GameHub(
        ILogger<GameHub> logger,
        IConnectionMapping connectionMapping,
        IRoomRepository roomRepo,
        IRoomPlayerRepository playerRepo)
    {
        _logger = logger;
        _connectionMapping = connectionMapping;
        _roomRepo = roomRepo;
        _playerRepo = playerRepo;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var info = _connectionMapping.Get(Context.ConnectionId);
        if (info is not null)
        {
            _connectionMapping.Remove(Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{info.RoomCode}");

            if (!info.IsHost)
            {
                // Xóa player khỏi DB khi disconnect
                await _playerRepo.DeleteAsync(info.PlayerId);

                var playerCount = await GetRoomPlayerCount(info.RoomCode);
                await Clients.Group($"Room_{info.RoomCode}")
                    .SendAsync("PlayerLeft", info.Nickname, playerCount);
                _logger.LogInformation("Player {Nickname} disconnected from room {RoomCode}", info.Nickname, info.RoomCode);
            }
            else
            {
                _logger.LogInformation("Host disconnected from room {RoomCode}", info.RoomCode);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Host tham gia phong de nhan real-time events
    /// </summary>
    public async Task JoinRoomAsHost(string roomCode)
    {
        var room = await _roomRepo.GetByCodeAsync(roomCode);
        if (room is null)
        {
            throw new HubException("Phong khong ton tai");
        }

        var groupName = $"Room_{roomCode}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _connectionMapping.Add(Context.ConnectionId, new ConnectionInfo(roomCode, room.HostId, "Host", true));

        _logger.LogInformation("Host joined room {RoomCode}, ConnectionId: {ConnectionId}", roomCode, Context.ConnectionId);
    }

    /// <summary>
    /// Nguoi choi tham gia phong qua SignalR
    /// </summary>
    public async Task JoinRoom(string roomCode, Guid playerId)
    {
        var room = await _roomRepo.GetByCodeAsync(roomCode);
        if (room is null)
        {
            throw new HubException("Phong khong ton tai");
        }

        var player = await _playerRepo.GetByIdAsync(playerId);
        if (player is null || player.RoomId != room.Id)
        {
            throw new HubException("Nguoi choi khong hop le");
        }

        // Update connection info
        player.ConnectionId = Context.ConnectionId;
        player.IsConnected = true;
        await _playerRepo.UpdateAsync(player);

        var groupName = $"Room_{roomCode}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _connectionMapping.Add(Context.ConnectionId, new ConnectionInfo(roomCode, playerId, player.Nickname, false));

        var playerCount = await GetRoomPlayerCount(roomCode);
        await Clients.Group(groupName).SendAsync("PlayerJoined", player.Nickname, playerCount);

        _logger.LogInformation("Player {Nickname} joined room {RoomCode}", player.Nickname, roomCode);
    }

    /// <summary>
    /// Nguoi choi roi phong
    /// </summary>
    public async Task LeaveRoom(string roomCode)
    {
        var info = _connectionMapping.Get(Context.ConnectionId);
        if (info is null || info.RoomCode != roomCode) return;

        _connectionMapping.Remove(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{roomCode}");

        if (!info.IsHost)
        {
            // Remove player from DB
            await _playerRepo.DeleteAsync(info.PlayerId);
            var playerCount = await GetRoomPlayerCount(roomCode);
            await Clients.Group($"Room_{roomCode}")
                .SendAsync("PlayerLeft", info.Nickname, playerCount);
        }

        _logger.LogInformation("{Role} {Nickname} left room {RoomCode}",
            info.IsHost ? "Host" : "Player", info.Nickname, roomCode);
    }

    /// <summary>
    /// Host kick nguoi choi
    /// </summary>
    public async Task KickPlayer(string roomCode, Guid playerId)
    {
        var hostInfo = _connectionMapping.Get(Context.ConnectionId);
        if (hostInfo is null || !hostInfo.IsHost || hostInfo.RoomCode != roomCode)
        {
            throw new HubException("Khong co quyen kick");
        }

        var player = await _playerRepo.GetByIdAsync(playerId);
        if (player is null) return;

        // Notify the kicked player
        var playerConnectionId = _connectionMapping.GetConnectionId(playerId);
        if (playerConnectionId is not null)
        {
            await Clients.Client(playerConnectionId).SendAsync("Kicked", "Ban da bi kick khoi phong");
            _connectionMapping.Remove(playerConnectionId);
            await Groups.RemoveFromGroupAsync(playerConnectionId, $"Room_{roomCode}");
        }

        // Remove from DB
        await _playerRepo.DeleteAsync(playerId);

        var playerCount = await GetRoomPlayerCount(roomCode);
        await Clients.Group($"Room_{roomCode}")
            .SendAsync("PlayerLeft", player.Nickname, playerCount);

        _logger.LogInformation("Host kicked player {Nickname} from room {RoomCode}", player.Nickname, roomCode);
    }

    private async Task<int> GetRoomPlayerCount(string roomCode)
    {
        var room = await _roomRepo.GetByCodeAsync(roomCode);
        if (room is null) return 0;
        return await _playerRepo.CountByRoomIdAsync(room.Id);
    }
}
