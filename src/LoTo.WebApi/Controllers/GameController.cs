using LoTo.Application.DTOs;
using LoTo.Application.Interfaces;
using LoTo.Application.UseCases.Games;
using LoTo.Domain.Interfaces;
using LoTo.Infrastructure.SignalR.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace LoTo.WebApi.Controllers;

[ApiController]
[Route("api/rooms/{roomCode}")]
public class GameController : ControllerBase
{
    private readonly StartGameUseCase _startGameUseCase;
    private readonly DrawNumberUseCase _drawNumberUseCase;
    private readonly ClaimKinhUseCase _claimKinhUseCase;
    private readonly EndGameUseCase _endGameUseCase;
    private readonly IRoomRepository _roomRepo;
    private readonly IGameSessionRepository _sessionRepo;
    private readonly IDrawnNumberRepository _drawnNumberRepo;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IConnectionMapping _connectionMapping;
    private readonly ILogger<GameController> _logger;

    public GameController(
        StartGameUseCase startGameUseCase,
        DrawNumberUseCase drawNumberUseCase,
        ClaimKinhUseCase claimKinhUseCase,
        EndGameUseCase endGameUseCase,
        IRoomRepository roomRepo,
        IGameSessionRepository sessionRepo,
        IDrawnNumberRepository drawnNumberRepo,
        IHubContext<GameHub> hubContext,
        IConnectionMapping connectionMapping,
        ILogger<GameController> logger)
    {
        _startGameUseCase = startGameUseCase;
        _drawNumberUseCase = drawNumberUseCase;
        _claimKinhUseCase = claimKinhUseCase;
        _endGameUseCase = endGameUseCase;
        _roomRepo = roomRepo;
        _sessionRepo = sessionRepo;
        _drawnNumberRepo = drawnNumberRepo;
        _hubContext = hubContext;
        _connectionMapping = connectionMapping;
        _logger = logger;
    }

    /// <summary>
    /// Bat dau game (Host only)
    /// </summary>
    [HttpPost("start")]
    [Authorize]
    [ProducesResponseType(typeof(StartGameResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> StartGame(string roomCode, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            var (response, tickets) = await _startGameUseCase.ExecuteAsync(userId, roomCode, ct);

            // Send each player their ticket via SignalR
            var connections = _connectionMapping.GetByRoom(roomCode);
            foreach (var conn in connections)
            {
                if (!conn.IsHost && tickets.TryGetValue(conn.PlayerId, out var ticket))
                {
                    var connId = _connectionMapping.GetConnectionId(conn.PlayerId);
                    if (connId is not null)
                    {
                        await _hubContext.Clients.Client(connId)
                            .SendAsync("GameStarted", ticket, response.GameSessionId, ct);
                    }
                }
            }

            // Notify all in room that game started
            await _hubContext.Clients.Group($"Room_{roomCode}")
                .SendAsync("GameStatusChanged", "playing", ct);

            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "ROOM_NOT_FOUND", message = "Phong khong ton tai" });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Khong co quyen" });
        }
        catch (InvalidOperationException ex)
        {
            var parts = ex.Message.Split(':', 2);
            var code = parts.Length == 2 ? parts[0] : "BAD_REQUEST";
            var message = parts.Length == 2 ? parts[1] : ex.Message;
            return BadRequest(new { error = code, message });
        }
    }

    /// <summary>
    /// Boc so (Host only)
    /// </summary>
    [HttpPost("draw")]
    [Authorize]
    [ProducesResponseType(typeof(DrawNumberResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DrawNumber(string roomCode, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            var result = await _drawNumberUseCase.ExecuteAsync(userId, roomCode, ct);

            // Broadcast to all players via SignalR
            await _hubContext.Clients.Group($"Room_{roomCode}")
                .SendAsync("NumberDrawn", result.Number, result.DrawOrder, ct);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "ROOM_NOT_FOUND", message = "Phong khong ton tai" });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Khong co quyen" });
        }
        catch (InvalidOperationException ex)
        {
            var parts = ex.Message.Split(':', 2);
            var code = parts.Length == 2 ? parts[0] : "BAD_REQUEST";
            var message = parts.Length == 2 ? parts[1] : ex.Message;
            return BadRequest(new { error = code, message });
        }
    }

    /// <summary>
    /// Bam KINH (Player)
    /// </summary>
    [HttpPost("kinh")]
    [ProducesResponseType(typeof(KinhClaimResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ClaimKinh(string roomCode, [FromBody] KinhClaimRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _claimKinhUseCase.ExecuteAsync(roomCode, request, ct);

            // Broadcast result to all in room
            await _hubContext.Clients.Group($"Room_{roomCode}")
                .SendAsync("KinhResult", result.Nickname, result.Valid, result.RowIndex, result.Message, ct);

            if (result.Valid)
            {
                await _hubContext.Clients.Group($"Room_{roomCode}")
                    .SendAsync("GameStatusChanged", "finished", ct);
            }

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "ROOM_NOT_FOUND", message = "Phong khong ton tai" });
        }
        catch (InvalidOperationException ex)
        {
            var parts = ex.Message.Split(':', 2);
            var code = parts.Length == 2 ? parts[0] : "BAD_REQUEST";
            var message = parts.Length == 2 ? parts[1] : ex.Message;
            return BadRequest(new { error = code, message });
        }
    }

    /// <summary>
    /// Ket thuc game (Host only)
    /// </summary>
    [HttpPost("end")]
    [Authorize]
    [ProducesResponseType(typeof(EndGameResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> EndGame(string roomCode, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            var result = await _endGameUseCase.ExecuteAsync(userId, roomCode, ct);

            await _hubContext.Clients.Group($"Room_{roomCode}")
                .SendAsync("GameEnded", result.WinnerNickname, result.TotalNumbersDrawn, ct);

            await _hubContext.Clients.Group($"Room_{roomCode}")
                .SendAsync("GameStatusChanged", "finished", ct);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "ROOM_NOT_FOUND", message = "Phong khong ton tai" });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Khong co quyen" });
        }
        catch (InvalidOperationException ex)
        {
            var parts = ex.Message.Split(':', 2);
            var code = parts.Length == 2 ? parts[0] : "BAD_REQUEST";
            var message = parts.Length == 2 ? parts[1] : ex.Message;
            return BadRequest(new { error = code, message });
        }
    }

    /// <summary>
    /// Lay trang thai game hien tai (Host only) - dung khi host reconnect
    /// </summary>
    [HttpGet("state")]
    [Authorize]
    [ProducesResponseType(typeof(GameStateResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetGameState(string roomCode, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var room = await _roomRepo.GetByCodeAsync(roomCode, ct);
        if (room is null)
            return NotFound(new { error = "ROOM_NOT_FOUND", message = "Phong khong ton tai" });

        if (room.HostId != userId)
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Khong co quyen" });

        var status = room.Status.ToString().ToLower();

        if (room.Status != Domain.Enums.RoomStatus.Playing)
            return Ok(new GameStateResponse(status, null, 0, null, []));

        var session = await _sessionRepo.GetActiveByRoomIdAsync(room.Id, ct);
        if (session is null)
            return Ok(new GameStateResponse(status, null, 0, null, []));

        var drawnNumbers = await _drawnNumberRepo.GetBySessionIdAsync(session.Id, ct);
        var numbers = drawnNumbers.OrderBy(d => d.DrawnOrder).Select(d => d.Number).ToList();
        var lastNumber = drawnNumbers.OrderByDescending(d => d.DrawnOrder).FirstOrDefault();

        return Ok(new GameStateResponse(
            status,
            session.Id,
            drawnNumbers.Count,
            lastNumber?.Number,
            numbers
        ));
    }
}
