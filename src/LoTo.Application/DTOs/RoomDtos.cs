namespace LoTo.Application.DTOs;

public record RoomInfoResponse(
    string RoomCode,
    string Status,
    string HostName,
    int PlayerCount,
    int MaxPlayers
);

public record JoinRoomRequest(string Nickname);

public record JoinRoomResponse(
    Guid PlayerId,
    string SessionToken,
    string RoomCode,
    string Nickname
);

public record CreateRoomResponse(
    Guid Id,
    string RoomCode,
    string Status,
    int MaxPlayers,
    DateTime CreatedAt
);

public record PlayerInfo(
    Guid Id,
    string Nickname,
    bool IsConnected
);

public record PlayersListResponse(
    List<PlayerInfo> Players,
    int PlayerCount,
    int MaxPlayers
);
