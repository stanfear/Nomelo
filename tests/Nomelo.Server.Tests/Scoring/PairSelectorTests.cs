using FluentAssertions;
using Nomelo.Server.Scoring;

namespace Nomelo.Server.Tests.Scoring;

public class PairSelectorTests
{
    private static ItemSnapshot Snap(string id, double elo = 1000, int shown = 0, bool banned = false)
        => new(id, elo, shown, banned);

    [Fact]
    public void Returns_null_if_fewer_than_two_eligible_items()
    {
        var items = new[] { Snap("a") };
        var result = PairSelector.Pick(items, recentPairs: Array.Empty<(string, string)>(), threshold: 3, rng: new Random(1));
        result.Should().BeNull();
    }

    [Fact]
    public void Excludes_banned_items()
    {
        var items = new[]
        {
            Snap("a", banned: true),
            Snap("b"),
            Snap("c")
        };

        for (int seed = 0; seed < 20; seed++)
        {
            var r = PairSelector.Pick(items, Array.Empty<(string, string)>(), 3, new Random(seed))!;
            r.A.Should().NotBe("a");
            r.B.Should().NotBe("a");
        }
    }

    [Fact]
    public void Returns_two_distinct_items()
    {
        var items = new[] { Snap("a"), Snap("b"), Snap("c") };

        for (int seed = 0; seed < 50; seed++)
        {
            var r = PairSelector.Pick(items, Array.Empty<(string, string)>(), 3, new Random(seed))!;
            r.A.Should().NotBe(r.B);
        }
    }

    [Fact]
    public void Avoids_pair_seen_in_recent_history_when_pool_is_large()
    {
        var items = Enumerable.Range(0, 10).Select(i => Snap($"i{i}")).ToArray();
        var recent = new[] { ("i0", "i1") };

        var hits = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var r = PairSelector.Pick(items, recent, 3, new Random(seed))!;
            if ((r.A == "i0" && r.B == "i1") || (r.A == "i1" && r.B == "i0")) hits++;
        }
        hits.Should().BeLessThan(5);
    }

    [Fact]
    public void Falls_back_to_repeating_pair_when_only_two_items_remain()
    {
        var items = new[] { Snap("a"), Snap("b") };
        var recent = new[] { ("a", "b") };

        var r = PairSelector.Pick(items, recent, 3, new Random(1))!;

        new[] { r.A, r.B }.OrderBy(x => x).Should().Equal("a", "b");
    }

    [Fact]
    public void With_two_items_both_appear_every_time()
    {
        var items = new[]
        {
            Snap("unseen", elo: 1000, shown: 0),
            Snap("neutral", elo: 900, shown: 50)
        };

        int unseenCount = 0;
        for (int seed = 0; seed < 500; seed++)
        {
            var r = PairSelector.Pick(items, Array.Empty<(string, string)>(), 3, new Random(seed))!;
            if (r.A == "unseen" || r.B == "unseen") unseenCount++;
        }
        unseenCount.Should().Be(500);
    }
}
