namespace Nomelo.Shared.Dtos;

public record ResultsDto(
    Guid SessionId,
    string ListId,
    string ListName,
    int VoteCount,
    bool StabilityReached,
    IReadOnlyList<RankedItemDto> Ranked,
    IReadOnlyList<RankedItemDto> Banned);
