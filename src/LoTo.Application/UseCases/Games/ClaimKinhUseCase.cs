using LoTo.Application.DTOs;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LoTo.Application.UseCases.Games;

public record KinhClaimRequest(Guid TicketId, int RowIndex);

public record KinhClaimResponse(bool Valid, string Nickname, int RowIndex, string Message);

public class ClaimKinhUseCase
{
    private readonly IRoomRepository _roomRepo;
    private readonly IGameSessionRepository _sessionRepo;
    private readonly ITicketRepository _ticketRepo;
    private readonly IDrawnNumberRepository _drawnRepo;
    private readonly IRoomPlayerRepository _playerRepo;
    private readonly ILogger<ClaimKinhUseCase> _logger;

    public ClaimKinhUseCase(
        IRoomRepository roomRepo,
        IGameSessionRepository sessionRepo,
        ITicketRepository ticketRepo,
        IDrawnNumberRepository drawnRepo,
        IRoomPlayerRepository playerRepo,
        ILogger<ClaimKinhUseCase> logger)
    {
        _roomRepo = roomRepo;
        _sessionRepo = sessionRepo;
        _ticketRepo = ticketRepo;
        _drawnRepo = drawnRepo;
        _playerRepo = playerRepo;
        _logger = logger;
    }

    public async Task<KinhClaimResponse> ExecuteAsync(string roomCode, KinhClaimRequest request, CancellationToken ct)
    {
        var room = await _roomRepo.GetByCodeAsync(roomCode, ct)
            ?? throw new KeyNotFoundException("Phong khong ton tai");

        if (room.Status != RoomStatus.Playing)
            throw new InvalidOperationException("GAME_NOT_STARTED:Game chua bat dau");

        var session = await _sessionRepo.GetActiveByRoomIdAsync(room.Id, ct)
            ?? throw new InvalidOperationException("GAME_NOT_STARTED:Khong tim thay game session");

        if (session.WinnerPlayerId.HasValue)
            throw new InvalidOperationException("GAME_ENDED:Game da ket thuc");

        var ticket = await _ticketRepo.GetByIdAsync(request.TicketId, ct)
            ?? throw new KeyNotFoundException("Ve khong ton tai");

        if (ticket.GameSessionId != session.Id)
            throw new InvalidOperationException("BAD_REQUEST:Ve khong thuoc game nay");

        if (request.RowIndex < 0 || request.RowIndex > 17)
            throw new InvalidOperationException("INVALID_KINH:Hang khong hop le");

        var player = await _playerRepo.GetByIdAsync(ticket.PlayerId, ct)
            ?? throw new KeyNotFoundException("Nguoi choi khong ton tai");

        // Parse ticket grid
        var gridDoc = JsonDocument.Parse(ticket.Grid);
        var rowsArray = gridDoc.RootElement.GetProperty("rows");
        var row = rowsArray[request.RowIndex];

        // Get numbers in the claimed row
        var rowNumbers = new List<int>();
        foreach (var cell in row.EnumerateArray())
        {
            if (cell.ValueKind == JsonValueKind.Number)
                rowNumbers.Add(cell.GetInt32());
        }

        // Get all drawn numbers
        var drawnNumbers = await _drawnRepo.GetBySessionIdAsync(session.Id, ct);
        var drawnSet = drawnNumbers.Select(d => d.Number).ToHashSet();

        // Check if all numbers in the row have been drawn
        var allDrawn = rowNumbers.All(n => drawnSet.Contains(n));

        if (!allDrawn)
        {
            _logger.LogInformation("Invalid KINH from {Nickname} in room {RoomCode}: row {Row} missing numbers",
                player.Nickname, roomCode, request.RowIndex);
            return new KinhClaimResponse(false, player.Nickname, request.RowIndex, "Chua du so, choi tiep!");
        }

        // Valid KINH - set winner
        session.WinnerPlayerId = ticket.PlayerId;
        session.WinnerRow = request.RowIndex;
        session.EndedAt = DateTime.UtcNow;
        await _sessionRepo.UpdateAsync(session, ct);

        // Update room status
        room.Status = RoomStatus.Finished;
        await _roomRepo.UpdateAsync(room, ct);

        _logger.LogInformation("KINH! {Nickname} thang o room {RoomCode}, row {Row}",
            player.Nickname, roomCode, request.RowIndex);

        return new KinhClaimResponse(true, player.Nickname, request.RowIndex,
            $"KINH! {player.Nickname} thang!");
    }
}
