namespace Nomelo.Shared.Dtos;

public record PairItemDto(
    string Value,
    IReadOnlyList<string> Variants,
    string? Description,
    string? Sparkline,
    int? PeakYear,
    int? PeakCount);

public record PairDto(PairItemDto A, PairItemDto B);
