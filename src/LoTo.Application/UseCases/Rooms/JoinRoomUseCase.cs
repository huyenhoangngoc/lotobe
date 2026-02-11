using LoTo.Application.DTOs;
using LoTo.Application.Interfaces;
using LoTo.Domain.Entities;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;

namespace LoTo.Application.UseCases.Rooms;

public class JoinRoomUseCase
{
    private readonly IRoomRepository _roomRepo;
    private readonly IRoomPlayerRepository _playerRepo;
    private readonly IUserRepository _userRepo;
    private readonly IJwtService _jwtService;

    public JoinRoomUseCase(
        IRoomRepository roomRepo,
        IRoomPlayerRepository playerRepo,
        IUserRepository userRepo,
        IJwtService jwtService)
    {
        _roomRepo = roomRepo;
        _playerRepo = playerRepo;
        _userRepo = userRepo;
        _jwtService = jwtService;
    }

    public async Task<RoomInfoResponse> GetRoomInfoAsync(string roomCode, CancellationToken ct)
    {
        var room = await _roomRepo.GetByCodeAsync(roomCode, ct)
            ?? throw new KeyNotFoundException("Phong khong ton tai");

        var playerCount = await _playerRepo.CountByRoomIdAsync(room.Id, ct);
        var host = await _userRepo.GetByIdAsync(room.HostId, ct);

        return new RoomInfoResponse(
            room.RoomCode,
            room.Status.ToString().ToLower(),
            host?.DisplayName ?? "Unknown",
            playerCount,
            room.MaxPlayers
        );
    }

    public async Task<JoinRoomResponse> JoinAsync(string roomCode, string nickname, CancellationToken ct)
    {
        var room = await _roomRepo.GetByCodeAsync(roomCode, ct)
            ?? throw new KeyNotFoundException("Phong khong ton tai");

        if (room.Status == RoomStatus.Playing)
            throw new InvalidOperationException("GAME_STARTED:Game da bat dau");

        if (room.Status == RoomStatus.Finished)
            throw new KeyNotFoundException("Phong khong ton tai");

        var playerCount = await _playerRepo.CountByRoomIdAsync(room.Id, ct);
        if (playerCount >= room.MaxPlayers)
            throw new InvalidOperationException("ROOM_FULL:Phong da day");

        if (await _playerRepo.NicknameExistsInRoomAsync(room.Id, nickname, ct))
            throw new InvalidOperationException("NICKNAME_TAKEN:Nickname da duoc su dung");

        var player = new RoomPlayer
        {
            RoomId = room.Id,
            Nickname = nickname,
        };
        player = await _playerRepo.CreateAsync(player, ct);

        // Generate a session token for the player (reuse JWT service with a player-specific claim)
        var tokens = _jwtService.GenerateTokens(player.Id, $"player:{nickname}", "player");

        return new JoinRoomResponse(player.Id, tokens.Token, roomCode, nickname);
    }
}
