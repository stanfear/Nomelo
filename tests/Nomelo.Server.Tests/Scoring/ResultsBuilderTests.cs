using FluentAssertions;
using Nomelo.Server.Lists;
using Nomelo.Server.Scoring;

namespace Nomelo.Server.Tests.Scoring;

public class ResultsBuilderTests
{
    private static ListFile List(params string[] values)
        => new("l", "L", values.Select(v => new ListFileItem(v, Array.Empty<string>(), null, null, null, null)).ToList());

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

    [Fact]
    public void Propagates_sparkline_metadata_to_ranked_item()
    {
        var list = new ListFile("l", "L", new[]
        {
            new ListFileItem("Catherine", Array.Empty<string>(), null, "▆▇█▇▅", 1963, 95245),
            new ListFileItem("Bob", Array.Empty<string>(), null, null, null, null)
        });
        var states = new Dictionary<string, (double, int, bool)>
        {
            ["Catherine"] = (1100, 5, false),
            ["Bob"] = (900, 5, false),
        };

        var (ranked, _) = ResultsBuilder.Build(list, states);

        var catherine = ranked.Single(r => r.Value == "Catherine");
        catherine.Sparkline.Should().Be("▆▇█▇▅");
        catherine.PeakYear.Should().Be(1963);
        catherine.PeakCount.Should().Be(95245);
        ranked.Single(r => r.Value == "Bob").Sparkline.Should().BeNull();
    }

    [Fact]
    public void Tied_items_ordered_by_times_shown_descending()
    {
        // Three items tied on ELO. They should be listed most-shown first so
        // the rank-group leader is the best-tested entry.
        var list = List("a", "b", "c");
        var states = new Dictionary<string, (double, int, bool)>
        {
            ["a"] = (1000, 2, false),
            ["b"] = (1000, 8, false),
            ["c"] = (1000, 5, false),
        };

        var (ranked, _) = ResultsBuilder.Build(list, states);

        ranked.Select(r => r.Value).Should().Equal("b", "c", "a");
        ranked.Select(r => r.Rank).Should().Equal(1, 1, 1);
    }
}
