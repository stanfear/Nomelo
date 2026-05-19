namespace Nomelo.Server.Scoring;

public record ItemSnapshot(string Id, double Elo, int TimesShown, bool IsBanned);

public record PickedPair(string A, string B);

public static class PairSelector
{
    private const int MaxRedraws = 10;

    public static PickedPair? Pick(
        IReadOnlyList<ItemSnapshot> items,
        IReadOnlyCollection<(string A, string B)> recentPairs,
        int threshold,
        Random rng)
    {
        var weighted = items
            .Where(i => !i.IsBanned)
            .Select(i => (i.Id, Weight: WeightCalculator.Weight(i.Elo, i.TimesShown, i.IsBanned, threshold)))
            .Where(x => x.Weight > 0)
            .ToList();

        if (weighted.Count < 2) return null;

        var recentSet = new HashSet<(string, string)>();
        foreach (var p in recentPairs)
        {
            recentSet.Add((p.A, p.B));
            recentSet.Add((p.B, p.A));
        }

        for (int attempt = 0; attempt < MaxRedraws; attempt++)
        {
            var a = DrawOne(weighted, rng);
            var remaining = weighted.Where(x => x.Id != a).ToList();
            var b = DrawOne(remaining, rng);

            if (!recentSet.Contains((a, b))) return new PickedPair(a, b);
        }

        var fa = DrawOne(weighted, rng);
        var fb = DrawOne(weighted.Where(x => x.Id != fa).ToList(), rng);
        return new PickedPair(fa, fb);
    }

    private static string DrawOne(List<(string Id, int Weight)> pool, Random rng)
    {
        var total = pool.Sum(x => x.Weight);
        var roll = rng.Next(total);
        var acc = 0;
        foreach (var (id, w) in pool)
        {
            acc += w;
            if (roll < acc) return id;
        }
        return pool[^1].Id;
    }
}
