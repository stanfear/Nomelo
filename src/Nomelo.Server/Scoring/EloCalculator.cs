namespace Nomelo.Server.Scoring;

public record EloUpdate(double NewEloA, double NewEloB);

public static class EloCalculator
{
    public static int KFactor(int timesShown) => timesShown switch
    {
        < 5 => 48,
        < 15 => 32,
        _ => 16
    };

    public static double ExpectedScore(double eloA, double eloB)
        => 1.0 / (1.0 + Math.Pow(10.0, (eloB - eloA) / 400.0));

    public static EloUpdate Apply(double eloA, double eloB, int kA, int kB, double scoreA)
    {
        var eA = ExpectedScore(eloA, eloB);
        var eB = 1.0 - eA;
        var scoreB = 1.0 - scoreA;
        return new EloUpdate(eloA + kA * (scoreA - eA), eloB + kB * (scoreB - eB));
    }
}
