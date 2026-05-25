using FluentAssertions;
using Nomelo.Server.Scoring;
using Xunit;

namespace Nomelo.Server.Tests.Scoring;

public class EloInverterTests
{
    // TimesShown values that span every KFactor bracket (48 / 32 / 16).
    private static readonly int[] TimesShownSamples = { 0, 1, 4, 5, 9, 14, 15, 30, 100 };
    // Elo offsets across the realistic working range.
    private static readonly double[] EloSamples =
    {
        700, 800, 900, 950, 1000, 1050, 1100, 1200, 1300, 1500
    };
    // Only Elo-affecting vote results — bans don't touch Elo.
    private static readonly double[] ScoreSamples = { 0.0, 0.5, 1.0 };

    public static IEnumerable<object[]> RoundTripCases()
    {
        foreach (var tsA in TimesShownSamples)
        foreach (var tsB in TimesShownSamples)
        foreach (var eloA in EloSamples)
        foreach (var eloB in EloSamples)
        foreach (var sA in ScoreSamples)
            yield return new object[] { tsA, tsB, eloA, eloB, sA };
    }

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void Forward_then_invert_recovers_original_elo(
        int timesShownBeforeA, int timesShownBeforeB,
        double eloABefore, double eloBBefore,
        double scoreA)
    {
        var kA = EloCalculator.KFactor(timesShownBeforeA);
        var kB = EloCalculator.KFactor(timesShownBeforeB);

        // Forward
        var updated = EloCalculator.Apply(eloABefore, eloBBefore, kA, kB, scoreA);

        // Inverse
        var prior = EloInverter.Invert(updated.NewEloA, updated.NewEloB, kA, kB, scoreA);

        // Tolerance: bisection runs 80 iterations on a [-K-1, K+1] bracket
        // with K ≤ 96, so the residual delta is well under 1e-10. Recovered
        // elos derive from one float multiplication so they stay tight.
        prior.EloABefore.Should().BeApproximately(eloABefore, 1e-6);
        prior.EloBBefore.Should().BeApproximately(eloBBefore, 1e-6);
    }

    [Fact]
    public void Invert_handles_equal_elos_PreferA()
    {
        var updated = EloCalculator.Apply(1000, 1000, 32, 32, 1.0);
        var prior = EloInverter.Invert(updated.NewEloA, updated.NewEloB, 32, 32, 1.0);
        prior.EloABefore.Should().BeApproximately(1000, 1e-6);
        prior.EloBBefore.Should().BeApproximately(1000, 1e-6);
    }

    [Fact]
    public void Invert_handles_asymmetric_K()
    {
        var updated = EloCalculator.Apply(1200, 900, 48, 16, 1.0);
        var prior = EloInverter.Invert(updated.NewEloA, updated.NewEloB, 48, 16, 1.0);
        prior.EloABefore.Should().BeApproximately(1200, 1e-6);
        prior.EloBBefore.Should().BeApproximately(900, 1e-6);
    }

    [Fact]
    public void Invert_handles_LikeBoth_between_distant_elos()
    {
        var updated = EloCalculator.Apply(1500, 700, 48, 48, 0.5);
        var prior = EloInverter.Invert(updated.NewEloA, updated.NewEloB, 48, 48, 0.5);
        prior.EloABefore.Should().BeApproximately(1500, 1e-6);
        prior.EloBBefore.Should().BeApproximately(700, 1e-6);
    }

    [Fact]
    public void Invert_handles_PreferB_underdog_upset()
    {
        // A was ahead, B wins — large swing for both
        var updated = EloCalculator.Apply(1300, 900, 32, 48, 0.0);
        var prior = EloInverter.Invert(updated.NewEloA, updated.NewEloB, 32, 48, 0.0);
        prior.EloABefore.Should().BeApproximately(1300, 1e-6);
        prior.EloBBefore.Should().BeApproximately(900, 1e-6);
    }
}
