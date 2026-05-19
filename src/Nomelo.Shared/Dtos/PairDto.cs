namespace Nomelo.Shared.Dtos;

public record PairItemDto(string Value, IReadOnlyList<string> Variants, string? Description);

public record PairDto(PairItemDto A, PairItemDto B);
