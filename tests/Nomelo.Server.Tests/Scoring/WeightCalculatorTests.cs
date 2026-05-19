using FluentAssertions;
using Nomelo.Server.Scoring;

namespace Nomelo.Server.Tests.Scoring;

public class WeightCalculatorTests
{
    [Fact]
    public void Banned_returns_zero()
        => WeightCalculator.Weight(elo: 1500, timesShown: 100, isBanned: true, threshold: 3).Should().Be(0);

    [Fact]
    public void Unseen_below_threshold_returns_60()
        => WeightCalculator.Weight(elo: 1000, timesShown: 0, isBanned: false, threshold: 3).Should().Be(60);

    [Fact]
    public void Unseen_at_threshold_minus_one_still_unseen()
        => WeightCalculator.Weight(elo: 1000, timesShown: 2, isBanned: false, threshold: 3).Should().Be(60);

    [Fact]
    public void Preferred_high_elo_above_1050_returns_25()
        => WeightCalculator.Weight(elo: 1100, timesShown: 10, isBanned: false, threshold: 3).Should().Be(25);

    [Fact]
    public void Neutral_elo_800_to_1050_returns_15()
    {
        WeightCalculator.Weight(elo: 800, timesShown: 10, isBanned: false, threshold: 3).Should().Be(15);
        WeightCalculator.Weight(elo: 1050, timesShown: 10, isBanned: false, threshold: 3).Should().Be(15);
    }

    [Fact]
    public void Cold_below_800_returns_5()
        => WeightCalculator.Weight(elo: 700, timesShown: 10, isBanned: false, threshold: 3).Should().Be(5);
}
