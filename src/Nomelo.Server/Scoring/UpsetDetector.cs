using Nomelo.Server.Data.Entities;

namespace Nomelo.Server.Scoring;

public static class UpsetDetector
{
    private const double LikeBothGapThreshold = 50.0;

    public static bool IsUpset(VoteResult result, double eloA, double eloB) => result switch
    {
        VoteResult.PreferA => eloA < eloB,
        VoteResult.PreferB => eloB < eloA,
        VoteResult.LikeBoth => Math.Abs(eloA - eloB) > LikeBothGapThreshold,
        _ => false
    };
}
