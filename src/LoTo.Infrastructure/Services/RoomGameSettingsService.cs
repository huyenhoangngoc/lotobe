using System.Collections.Concurrent;
using LoTo.Application.Interfaces;

namespace LoTo.Infrastructure.Services;

public class RoomGameSettingsService : IRoomGameSettings
{
    private readonly ConcurrentDictionary<string, RoomGameSettings> _settings = new();

    public RoomGameSettings Get(string roomCode)
    {
        return _settings.GetValueOrDefault(roomCode, new RoomGameSettings());
    }

    public void Update(string roomCode, RoomGameSettings settings)
    {
        _settings.AddOrUpdate(roomCode, settings, (_, _) => settings);
    }

    public void Remove(string roomCode)
    {
        _settings.TryRemove(roomCode, out _);
    }
}
