namespace Nomelo.Shared.Dtos;

public record SessionDto(
    Guid Id,
    string ListId,
    string ListName,
    int ConfidenceThreshold,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ShareToken,
    int VoteCount);
