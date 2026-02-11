using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace LoTo.Application.UseCases.Games;

public record EndGameResponse(Guid GameSessionId, string? WinnerNickname, int TotalNumbersDrawn);

public class EndGameUseCase
{
    private readonly IRoomRepository _roomRepo;
    private readonly IGameSessionRepository _sessionRepo;
    private readonly IRoomPlayerRepository _playerRepo;
    private readonly ILogger<EndGameUseCase> _logger;

    public EndGameUseCase(
        IRoomRepository roomRepo,
        IGameSessionRepository sessionRepo,
        IRoomPlayerRepository playerRepo,
        ILogger<EndGameUseCase> logger)
    {
        _roomRepo = roomRepo;
        _sessionRepo = sessionRepo;
        _playerRepo = playerRepo;
        _logger = logger;
    }

    public async Task<EndGameResponse> ExecuteAsync(Guid hostId, string roomCode, CancellationToken ct)
    {
        var room = await _roomRepo.GetByCodeAsync(roomCode, ct)
            ?? throw new KeyNotFoundException("Phong khong ton tai");

        if (room.HostId != hostId)
            throw new UnauthorizedAccessException("Khong co quyen ket thuc game");

        if (room.Status != RoomStatus.Playing)
            throw new InvalidOperationException("GAME_NOT_STARTED:Game chua bat dau");

        var session = await _sessionRepo.GetActiveByRoomIdAsync(room.Id, ct)
            ?? throw new InvalidOperationException("GAME_NOT_STARTED:Khong tim thay game session");

        // End session
        session.EndedAt = DateTime.UtcNow;
        await _sessionRepo.UpdateAsync(session, ct);

        // Update room status
        room.Status = RoomStatus.Finished;
        await _roomRepo.UpdateAsync(room, ct);

        // Get winner nickname if exists
        string? winnerNickname = null;
        if (session.WinnerPlayerId.HasValue)
        {
            var winner = await _playerRepo.GetByIdAsync(session.WinnerPlayerId.Value, ct);
            winnerNickname = winner?.Nickname;
        }

        _logger.LogInformation("Game ended in room {RoomCode}, {TotalDrawn} numbers drawn",
            roomCode, session.TotalNumbersDrawn);

        return new EndGameResponse(session.Id, winnerNickname, session.TotalNumbersDrawn);
    }
}
