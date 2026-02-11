using LoTo.Application.DTOs;
using LoTo.Domain.Entities;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace LoTo.Application.UseCases.Rooms;

public class CreateRoomUseCase
{
    private readonly IRoomRepository _roomRepo;
    private readonly IUserRepository _userRepo;
    private readonly IAppSettingsRepository _settingsRepo;
    private readonly ILogger<CreateRoomUseCase> _logger;

    public CreateRoomUseCase(
        IRoomRepository roomRepo,
        IUserRepository userRepo,
        IAppSettingsRepository settingsRepo,
        ILogger<CreateRoomUseCase> logger)
    {
        _roomRepo = roomRepo;
        _userRepo = userRepo;
        _settingsRepo = settingsRepo;
        _logger = logger;
    }

    public async Task<CreateRoomResponse> ExecuteAsync(Guid hostId, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(hostId, ct)
            ?? throw new UnauthorizedAccessException("User khong ton tai");

        if (user.IsBanned)
            throw new InvalidOperationException("Tai khoan da bi khoa");

        // Check if host already has an active room
        var activeRooms = await _roomRepo.GetActiveByHostIdAsync(hostId, ct);
        if (activeRooms.Count > 0)
        {
            // Return existing room instead of creating new one
            var existing = activeRooms[0];
            return new CreateRoomResponse(
                existing.Id,
                existing.RoomCode,
                existing.Status.ToString().ToLower(),
                existing.MaxPlayers,
                existing.CreatedAt
            );
        }

        var globalPremium = await _settingsRepo.IsGlobalPremiumEnabledAsync(ct);
        var maxPlayers = (user.IsPremium || globalPremium) ? 60 : 5;
        var roomCode = await GenerateUniqueCodeAsync(ct);

        var room = new Room
        {
            HostId = hostId,
            RoomCode = roomCode,
            Status = RoomStatus.Waiting,
            MaxPlayers = maxPlayers,
        };

        room = await _roomRepo.CreateAsync(room, ct);
        _logger.LogInformation("Room created: {RoomCode} by {HostId}", roomCode, hostId);

        return new CreateRoomResponse(
            room.Id,
            room.RoomCode,
            room.Status.ToString().ToLower(),
            room.MaxPlayers,
            room.CreatedAt
        );
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        var random = new Random();
        for (var i = 0; i < 100; i++)
        {
            var code = random.Next(0, 1000000).ToString("D6");
            var existing = await _roomRepo.GetByCodeAsync(code, ct);
            if (existing is null) return code;
        }
        throw new InvalidOperationException("Khong the tao ma phong");
    }
}
