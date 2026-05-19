namespace Nomelo.Server.Scoring;

public static class StabilityCounter
{
    public const int StabilityThreshold = 100;

    public static int ConsecutiveNonUpsets(IReadOnlyList<bool> isUpsetMostRecentFirst)
    {
        var count = 0;
        foreach (var isUpset in isUpsetMostRecentFirst)
        {
            if (isUpset) break;
            count++;
        }
        return count;
    }
}
