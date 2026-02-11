using System.Collections.Concurrent;
using LoTo.Application.Interfaces;

namespace LoTo.Infrastructure.Services;

public class ConnectionMapping : IConnectionMapping
{
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();

    public void Add(string connectionId, ConnectionInfo info)
    {
        _connections[connectionId] = info;
    }

    public void Remove(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    public ConnectionInfo? Get(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var info) ? info : null;
    }

    public List<ConnectionInfo> GetByRoom(string roomCode)
    {
        return _connections.Values
            .Where(c => c.RoomCode == roomCode)
            .ToList();
    }

    public string? GetConnectionId(Guid playerId)
    {
        return _connections.FirstOrDefault(kvp => kvp.Value.PlayerId == playerId).Key;
    }
}
