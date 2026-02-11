namespace LoTo.Application.DTOs;

public record StartGameResponse(
    Guid GameSessionId,
    DateTime StartedAt,
    int PlayerCount
);

public record TicketDto(
    Guid Id,
    int?[][] Rows,
    int[] MarkedNumbers
);

public record DrawNumberResponse(
    int Number,
    int DrawOrder,
    int RemainingNumbers,
    List<int> DrawnNumbers
);

public record GameStateResponse(
    string Status,
    Guid? GameSessionId,
    int DrawOrder,
    int? CurrentNumber,
    List<int> DrawnNumbers
);
