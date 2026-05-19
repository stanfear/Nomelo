using FluentAssertions;
using Nomelo.Server.Data.Entities;
using Nomelo.Server.Scoring;

namespace Nomelo.Server.Tests.Scoring;

public class UpsetDetectorTests
{
    [Fact]
    public void PreferA_with_lower_eloA_is_upset()
        => UpsetDetector.IsUpset(VoteResult.PreferA, eloA: 900, eloB: 1100).Should().BeTrue();

    [Fact]
    public void PreferA_with_higher_eloA_is_not_upset()
        => UpsetDetector.IsUpset(VoteResult.PreferA, eloA: 1100, eloB: 900).Should().BeFalse();

    [Fact]
    public void PreferA_with_equal_elos_is_not_upset()
        => UpsetDetector.IsUpset(VoteResult.PreferA, eloA: 1000, eloB: 1000).Should().BeFalse();

    [Fact]
    public void LikeBoth_with_large_gap_is_upset()
        => UpsetDetector.IsUpset(VoteResult.LikeBoth, eloA: 1100, eloB: 1000).Should().BeTrue();

    [Fact]
    public void LikeBoth_with_small_gap_is_not_upset()
        => UpsetDetector.IsUpset(VoteResult.LikeBoth, eloA: 1030, eloB: 1000).Should().BeFalse();

    [Theory]
    [InlineData(VoteResult.BanA)]
    [InlineData(VoteResult.BanB)]
    [InlineData(VoteResult.BanBoth)]
    public void Bans_are_never_upsets(VoteResult r)
        => UpsetDetector.IsUpset(r, 100, 2000).Should().BeFalse();
}
