namespace LoTo.Application.Interfaces;

public record RoomGameSettings(bool HideDrawnNumbers = false);

public interface IRoomGameSettings
{
    RoomGameSettings Get(string roomCode);
    void Update(string roomCode, RoomGameSettings settings);
    void Remove(string roomCode);
}
