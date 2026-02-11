namespace LoTo.Application.Interfaces;

public record ConnectionInfo(string RoomCode, Guid PlayerId, string Nickname, bool IsHost);

public interface IConnectionMapping
{
    void Add(string connectionId, ConnectionInfo info);
    void Remove(string connectionId);
    ConnectionInfo? Get(string connectionId);
    List<ConnectionInfo> GetByRoom(string roomCode);
    string? GetConnectionId(Guid playerId);
}
