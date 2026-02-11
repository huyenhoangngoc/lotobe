using LoTo.Application.DTOs;
using LoTo.Domain.Entities;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace LoTo.Application.UseCases.Games;

public class DrawNumberUseCase
{
    private readonly IRoomRepository _roomRepo;
    private readonly IGameSessionRepository _sessionRepo;
    private readonly IDrawnNumberRepository _drawnRepo;
    private readonly ILogger<DrawNumberUseCase> _logger;
    private static readonly Random _random = new();

    public DrawNumberUseCase(
        IRoomRepository roomRepo,
        IGameSessionRepository sessionRepo,
        IDrawnNumberRepository drawnRepo,
        ILogger<DrawNumberUseCase> logger)
    {
        _roomRepo = roomRepo;
        _sessionRepo = sessionRepo;
        _drawnRepo = drawnRepo;
        _logger = logger;
    }

    public async Task<DrawNumberResponse> ExecuteAsync(Guid hostId, string roomCode, CancellationToken ct)
    {
        var room = await _roomRepo.GetByCodeAsync(roomCode, ct)
            ?? throw new KeyNotFoundException("Phong khong ton tai");

        if (room.HostId != hostId)
            throw new UnauthorizedAccessException("Khong co quyen boc so");

        if (room.Status != RoomStatus.Playing)
            throw new InvalidOperationException("GAME_NOT_STARTED:Game chua bat dau");

        var session = await _sessionRepo.GetActiveByRoomIdAsync(room.Id, ct)
            ?? throw new InvalidOperationException("GAME_NOT_STARTED:Khong tim thay game session");

        // Get already drawn numbers
        var drawnNumbers = await _drawnRepo.GetBySessionIdAsync(session.Id, ct);
        var drawnSet = drawnNumbers.Select(d => d.Number).ToHashSet();

        if (drawnSet.Count >= 90)
            throw new InvalidOperationException("ALL_DRAWN:Da boc het 90 so");

        // Pick a random number not yet drawn
        int number;
        do
        {
            number = _random.Next(1, 91);
        } while (drawnSet.Contains(number));

        var drawOrder = drawnSet.Count + 1;

        // Save drawn number
        var drawn = new DrawnNumber
        {
            GameSessionId = session.Id,
            Number = number,
            DrawnOrder = drawOrder,
        };
        await _drawnRepo.CreateAsync(drawn, ct);

        // Update session total
        session.TotalNumbersDrawn = drawOrder;
        await _sessionRepo.UpdateAsync(session, ct);

        var allDrawn = drawnNumbers.Select(d => d.Number).Append(number).OrderBy(n => n).ToList();

        _logger.LogInformation("Drew number {Number} (#{Order}/90) in room {RoomCode}",
            number, drawOrder, roomCode);

        return new DrawNumberResponse(number, drawOrder, 90 - drawOrder, allDrawn);
    }
}
