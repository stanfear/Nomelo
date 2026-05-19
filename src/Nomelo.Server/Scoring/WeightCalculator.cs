namespace Nomelo.Server.Scoring;

public static class WeightCalculator
{
    public static int Weight(double elo, int timesShown, bool isBanned, int threshold)
    {
        if (isBanned) return 0;
        if (timesShown < threshold) return 60;
        if (elo > 1050) return 25;
        if (elo >= 800) return 15;
        return 5;
    }
}
