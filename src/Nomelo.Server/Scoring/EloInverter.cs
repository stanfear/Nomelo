namespace Nomelo.Server.Scoring;

public record EloPriorState(double EloABefore, double EloBBefore);

public static class EloInverter
{
    // Inverse of EloCalculator.Apply.
    // Given the post-vote elo and the K-factors actually used at vote time
    // (computed from TimesShown - 1), recovers the pre-vote elo.
    //
    // Forward:
    //   eA = 1 / (1 + 10^((eloB_before - eloA_before)/400))
    //   eloA_now = eloA_before + kA·(sA - eA)
    //   eloB_now = eloB_before - kB·(sA - eA)
    //
    // Let G = eloB_now - eloA_now, Δ = eloB_before - eloA_before, K = kA + kB.
    // Subtracting the two forward equations gives:
    //   Δ = G + K·(sA - eA(Δ))
    // One unknown, strictly monotonic in Δ → bisection.
    public static EloPriorState Invert(double eloANow, double eloBNow, int kA, int kB, double scoreA)
    {
        var gAfter = eloBNow - eloANow;
        var k = kA + kB;

        // |Δ_before - G_after| = |K·(sA - eA)| ≤ K (since sA, eA ∈ [0,1]).
        // Pad by 1 for safety against floating-point boundary checks.
        double lo = gAfter - k - 1.0;
        double hi = gAfter + k + 1.0;

        for (int i = 0; i < 80; i++)
        {
            var mid = 0.5 * (lo + hi);
            var eA = 1.0 / (1.0 + Math.Pow(10.0, mid / 400.0));
            var residual = mid - gAfter - k * (scoreA - eA);
            if (residual > 0) hi = mid;
            else lo = mid;
            if (hi - lo < 1e-10) break;
        }

        var delta = 0.5 * (lo + hi);
        var eaFinal = 1.0 / (1.0 + Math.Pow(10.0, delta / 400.0));
        var swing = scoreA - eaFinal;
        return new EloPriorState(eloANow - kA * swing, eloBNow + kB * swing);
    }
}
