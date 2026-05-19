using FluentAssertions;
using Nomelo.Server.Scoring;

namespace Nomelo.Server.Tests.Scoring;

public class EloCalculatorTests
{
    [Theory]
    [InlineData(0, 48)]
    [InlineData(4, 48)]
    [InlineData(5, 32)]
    [InlineData(14, 32)]
    [InlineData(15, 16)]
    [InlineData(100, 16)]
    public void K_factor_brackets(int timesShown, int expected)
    {
        EloCalculator.KFactor(timesShown).Should().Be(expected);
    }

    [Fact]
    public void Expected_score_equal_elos_is_half()
    {
        EloCalculator.ExpectedScore(1000, 1000).Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Expected_score_higher_elo_is_above_half()
    {
        EloCalculator.ExpectedScore(1200, 1000).Should().BeGreaterThan(0.5);
        EloCalculator.ExpectedScore(1000, 1200).Should().BeLessThan(0.5);
    }

    [Fact]
    public void PreferA_updates_both_elos_correctly()
    {
        var r = EloCalculator.Apply(eloA: 1000, eloB: 1000, kA: 32, kB: 32, scoreA: 1.0);
        r.NewEloA.Should().BeApproximately(1016, 0.01);
        r.NewEloB.Should().BeApproximately(984, 0.01);
    }

    [Fact]
    public void LikeBoth_pulls_elos_toward_each_other()
    {
        var r = EloCalculator.Apply(eloA: 1200, eloB: 1000, kA: 32, kB: 32, scoreA: 0.5);
        r.NewEloA.Should().BeLessThan(1200);
        r.NewEloB.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void Asymmetric_K_works()
    {
        var r = EloCalculator.Apply(eloA: 1000, eloB: 1000, kA: 48, kB: 16, scoreA: 1.0);
        (r.NewEloA - 1000).Should().BeApproximately(24, 0.01);
        (1000 - r.NewEloB).Should().BeApproximately(8, 0.01);
    }
}
