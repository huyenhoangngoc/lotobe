using LoTo.Application.DTOs;
using LoTo.Application.Services;
using LoTo.Domain.Entities;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace LoTo.Application.UseCases.Games;

public class StartGameUseCase
{
    private readonly IRoomRepository _roomRepo;
    private readonly IRoomPlayerRepository _playerRepo;
    private readonly IGameSessionRepository _sessionRepo;
    private readonly ITicketRepository _ticketRepo;
    private readonly ILogger<StartGameUseCase> _logger;

    public StartGameUseCase(
        IRoomRepository roomRepo,
        IRoomPlayerRepository playerRepo,
        IGameSessionRepository sessionRepo,
        ITicketRepository ticketRepo,
        ILogger<StartGameUseCase> logger)
    {
        _roomRepo = roomRepo;
        _playerRepo = playerRepo;
        _sessionRepo = sessionRepo;
        _ticketRepo = ticketRepo;
        _logger = logger;
    }

    /// <summary>
    /// Bat dau game: tao game session, generate tickets cho tat ca nguoi choi
    /// Returns: (StartGameResponse, Dictionary playerId -> TicketDto)
    /// </summary>
    public async Task<(StartGameResponse Response, Dictionary<Guid, TicketDto> Tickets)> ExecuteAsync(
        Guid hostId, string roomCode, CancellationToken ct)
    {
        var room = await _roomRepo.GetByCodeAsync(roomCode, ct)
            ?? throw new KeyNotFoundException("Phong khong ton tai");

        if (room.HostId != hostId)
            throw new UnauthorizedAccessException("Khong co quyen bat dau game");

        if (room.Status != RoomStatus.Waiting)
            throw new InvalidOperationException("GAME_STARTED:Game da bat dau roi");

        var players = await _playerRepo.GetByRoomIdAsync(room.Id, ct);
        if (players.Count == 0)
            throw new InvalidOperationException("BAD_REQUEST:Can it nhat 1 nguoi choi de bat dau");

        // Create game session
        var session = new GameSession
        {
            RoomId = room.Id,
        };
        session = await _sessionRepo.CreateAsync(session, ct);

        // Generate tickets for each player
        var tickets = new Dictionary<Guid, TicketDto>();
        foreach (var player in players)
        {
            var grid = TicketGenerator.Generate();
            var ticket = new Ticket
            {
                GameSessionId = session.Id,
                PlayerId = player.Id,
                Grid = TicketGenerator.SerializeGrid(grid),
                MarkedNumbers = [],
            };
            ticket = await _ticketRepo.CreateAsync(ticket, ct);
            tickets[player.Id] = new TicketDto(ticket.Id, grid, []);
        }

        // Update room status
        room.Status = RoomStatus.Playing;
        await _roomRepo.UpdateAsync(room, ct);

        _logger.LogInformation("Game started in room {RoomCode} with {PlayerCount} players, Session: {SessionId}",
            roomCode, players.Count, session.Id);

        var response = new StartGameResponse(session.Id, session.StartedAt, players.Count);
        return (response, tickets);
    }
}
