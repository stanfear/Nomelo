using FluentAssertions;
using Nomelo.Server.Lists;
using Nomelo.Server.Scoring;

namespace Nomelo.Server.Tests.Scoring;

public class ResultsBuilderTests
{
    private static ListFile List(params string[] values)
        => new("l", "L", values.Select(v => new ListFileItem(v, Array.Empty<string>(), null)).ToList());

    [Fact]
    public void Ranks_non_banned_by_elo_descending()
    {
        var list = List("a", "b", "c");
        var states = new Dictionary<string, (double elo, int shown, bool banned)>
        {
            ["a"] = (1100, 5, false),
            ["b"] = (900, 5, false),
            ["c"] = (1000, 5, false)
        };

        var (ranked, banned) = ResultsBuilder.Build(list, states);

        ranked.Select(r => r.Value).Should().Equal("a", "c", "b");
        ranked[0].Rank.Should().Be(1);
        ranked[2].Rank.Should().Be(3);
        banned.Should().BeEmpty();
    }

    [Fact]
    public void Banned_items_in_separate_list()
    {
        var list = List("a", "b");
        var states = new Dictionary<string, (double, int, bool)>
        {
            ["a"] = (1000, 5, true),
            ["b"] = (1000, 5, false)
        };

        var (ranked, banned) = ResultsBuilder.Build(list, states);

        ranked.Should().ContainSingle(r => r.Value == "b");
        banned.Should().ContainSingle(r => r.Value == "a");
    }

    [Fact]
    public void Items_without_state_default_to_1000_and_unranked_at_back()
    {
        var list = List("a", "b");
        var states = new Dictionary<string, (double, int, bool)>
        {
            ["a"] = (1200, 5, false)
        };

        var (ranked, _) = ResultsBuilder.Build(list, states);

        ranked.Select(r => r.Value).Should().Equal("a", "b");
        ranked[1].EloScore.Should().Be(1000);
        ranked[1].TimesShown.Should().Be(0);
    }

    [Fact]
    public void Ties_share_lower_rank_and_next_rank_skips()
    {
        // Five items, two tied at the 3rd position. Expected ranks: 1, 2, 3, 3, 5.
        var list = List("a", "b", "c", "d", "e");
        var states = new Dictionary<string, (double, int, bool)>
        {
            ["a"] = (1200, 5, false),
            ["b"] = (1100, 5, false),
            ["c"] = (1000, 5, false),
            ["d"] = (1000, 5, false),
            ["e"] = (900,  5, false),
        };

        var (ranked, _) = ResultsBuilder.Build(list, states);

        ranked.Select(r => r.Rank).Should().Equal(1, 2, 3, 3, 5);
    }

    [Fact]
    public void Ties_tied_at_top()
    {
        // Three items tied at top, then one alone. Expected ranks: 1, 1, 1, 4.
        var list = List("a", "b", "c", "d");
        var states = new Dictionary<string, (double, int, bool)>
        {
            ["a"] = (1100, 5, false),
            ["b"] = (1100, 5, false),
            ["c"] = (1100, 5, false),
            ["d"] = (900,  5, false),
        };

        var (ranked, _) = ResultsBuilder.Build(list, states);

        ranked.Select(r => r.Rank).Should().Equal(1, 1, 1, 4);
    }
}
