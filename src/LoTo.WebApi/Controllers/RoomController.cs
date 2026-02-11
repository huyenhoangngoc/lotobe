using LoTo.Application.DTOs;
using LoTo.Application.UseCases.Rooms;
using LoTo.Domain.Enums;
using LoTo.Domain.Interfaces;
using LoTo.Infrastructure.SignalR.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace LoTo.WebApi.Controllers;

[ApiController]
[Route("api/rooms")]
public class RoomController : ControllerBase
{
    private readonly JoinRoomUseCase _joinRoomUseCase;
    private readonly CreateRoomUseCase _createRoomUseCase;
    private readonly IRoomRepository _roomRepo;
    private readonly IRoomPlayerRepository _playerRepo;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<RoomController> _logger;

    public RoomController(
        JoinRoomUseCase joinRoomUseCase,
        CreateRoomUseCase createRoomUseCase,
        IRoomRepository roomRepo,
        IRoomPlayerRepository playerRepo,
        IHubContext<GameHub> hubContext,
        ILogger<RoomController> logger)
    {
        _joinRoomUseCase = joinRoomUseCase;
        _createRoomUseCase = createRoomUseCase;
        _roomRepo = roomRepo;
        _playerRepo = playerRepo;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Tao phong moi (Host only)
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CreateRoomResponse), 201)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreateRoom(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            var result = await _createRoomUseCase.ExecuteAsync(userId, ct);
            return StatusCode(201, result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Khong co quyen" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "BAD_REQUEST", message = ex.Message });
        }
    }

    /// <summary>
    /// Lay phong dang hoat dong cua host hien tai
    /// </summary>
    [HttpGet("my-active")]
    [Authorize]
    [ProducesResponseType(typeof(CreateRoomResponse), 200)]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetMyActiveRoom(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var activeRooms = await _roomRepo.GetActiveByHostIdAsync(userId, ct);
        if (activeRooms.Count == 0)
            return NoContent();

        var room = activeRooms[0];
        return Ok(new CreateRoomResponse(
            room.Id,
            room.RoomCode,
            room.Status.ToString().ToLower(),
            room.MaxPlayers,
            room.CreatedAt
        ));
    }

    /// <summary>
    /// Lay thong tin phong (public)
    /// </summary>
    [HttpGet("{roomCode}")]
    [ProducesResponseType(typeof(RoomInfoResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetRoom(string roomCode, CancellationToken ct)
    {
        try
        {
            var room = await _joinRoomUseCase.GetRoomInfoAsync(roomCode, ct);
            return Ok(room);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "ROOM_NOT_FOUND", message = "Phong khong ton tai" });
        }
    }

    /// <summary>
    /// Nguoi choi tham gia phong
    /// </summary>
    [HttpPost("{roomCode}/join")]
    [ProducesResponseType(typeof(JoinRoomResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> JoinRoom(string roomCode, [FromBody] JoinRoomRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nickname) || request.Nickname.Length > 20)
            return BadRequest(new { error = "INVALID_INPUT", message = "Nickname khong hop le (1-20 ky tu)" });

        try
        {
            var result = await _joinRoomUseCase.JoinAsync(roomCode, request.Nickname.Trim(), ct);
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
    /// Lay danh sach nguoi choi trong phong
    /// </summary>
    [HttpGet("{roomCode}/players")]
    [ProducesResponseType(typeof(PlayersListResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPlayers(string roomCode, CancellationToken ct)
    {
        var room = await _roomRepo.GetByCodeAsync(roomCode, ct);
        if (room is null)
            return NotFound(new { error = "ROOM_NOT_FOUND", message = "Phong khong ton tai" });

        var players = await _playerRepo.GetByRoomIdAsync(room.Id, ct);
        var response = new PlayersListResponse(
            players.Select(p => new PlayerInfo(p.Id, p.Nickname, p.IsConnected)).ToList(),
            players.Count,
            room.MaxPlayers
        );
        return Ok(response);
    }

    /// <summary>
    /// Dong phong (Host only) - hoat dong o moi trang thai
    /// </summary>
    [HttpPost("{roomCode}/close")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CloseRoom(string roomCode, CancellationToken ct)
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

        room.Status = RoomStatus.Finished;
        room.ClosedAt = DateTime.UtcNow;
        await _roomRepo.UpdateAsync(room, ct);

        // Thong bao tat ca nguoi choi phong da dong
        await _hubContext.Clients.Group($"Room_{roomCode}")
            .SendAsync("RoomClosed", ct);

        _logger.LogInformation("Room {RoomCode} closed by host {HostId}", roomCode, userId);

        return NoContent();
    }
}
