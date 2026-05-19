using Nomelo.Server.Lists;
using Nomelo.Shared.Dtos;

namespace Nomelo.Server.Scoring;

public static class ResultsBuilder
{
    public static (IReadOnlyList<RankedItemDto> Ranked, IReadOnlyList<RankedItemDto> Banned) Build(
        ListFile list,
        IReadOnlyDictionary<string, (double Elo, int TimesShown, bool IsBanned)> states)
    {
        var items = list.Items.Select(i =>
        {
            var has = states.TryGetValue(i.Value, out var s);
            return new
            {
                Item = i,
                Elo = has ? s.Elo : 1000.0,
                Shown = has ? s.TimesShown : 0,
                Banned = has && s.IsBanned
            };
        }).ToList();

        var ranked = items
            .Where(x => !x.Banned)
            .OrderByDescending(x => x.Elo)
            .Select((x, idx) => new RankedItemDto(
                idx + 1, x.Item.Value, x.Item.Variants, x.Elo, x.Shown, false))
            .ToList();

        var banned = items
            .Where(x => x.Banned)
            .OrderBy(x => x.Item.Value)
            .Select(x => new RankedItemDto(
                0, x.Item.Value, x.Item.Variants, x.Elo, x.Shown, true))
            .ToList();

        return (ranked, banned);
    }
}
