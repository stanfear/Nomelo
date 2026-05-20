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

        var sorted = items
            .Where(x => !x.Banned)
            .OrderByDescending(x => x.Elo)
            .ToList();

        // Standard "competition" ranking (1224): tied items share the lower
        // rank, the next non-tied item's rank equals (count of items above) + 1.
        // Tie key uses the rounded ELO so it matches what the UI displays.
        var ranked = new List<RankedItemDto>(sorted.Count);
        double? lastKey = null;
        int currentRank = 0;
        for (var i = 0; i < sorted.Count; i++)
        {
            var x = sorted[i];
            var key = Math.Round(x.Elo);
            if (lastKey is null || key != lastKey)
            {
                currentRank = i + 1;
                lastKey = key;
            }
            ranked.Add(new RankedItemDto(
                currentRank, x.Item.Value, x.Item.Variants, x.Elo, x.Shown, false));
        }

        var banned = items
            .Where(x => x.Banned)
            .OrderBy(x => x.Item.Value)
            .Select(x => new RankedItemDto(
                0, x.Item.Value, x.Item.Variants, x.Elo, x.Shown, true))
            .ToList();

        return (ranked, banned);
    }
}
