namespace Nomelo.Shared.Dtos;

public record RankedItemDto(
    int Rank,
    string Value,
    IReadOnlyList<string> Variants,
    double EloScore,
    int TimesShown,
    bool IsBanned);
