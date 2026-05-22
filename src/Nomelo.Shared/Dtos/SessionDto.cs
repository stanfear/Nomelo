namespace Nomelo.Shared.Dtos;

public record SessionDto(
    Guid Id,
    string ListId,
    string ListName,
    string? Name,
    int ConfidenceThreshold,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ShareToken,
    int VoteCount,
    bool StabilityReached);
