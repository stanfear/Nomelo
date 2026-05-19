using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Data;
using Nomelo.Server.Data.Entities;
using Nomelo.Server.Scoring;

namespace Nomelo.Server.Voting;

public class VoteProcessor(AppDbContext db)
{
    public async Task Apply(Guid sessionId, string itemA, string itemB, VoteResult result, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
                      ?? throw new InvalidOperationException($"session {sessionId} not found");

        var stateA = await GetOrCreate(sessionId, itemA, ct);
        var stateB = await GetOrCreate(sessionId, itemB, ct);

        var kA = EloCalculator.KFactor(stateA.TimesShown);
        var kB = EloCalculator.KFactor(stateB.TimesShown);

        stateA.TimesShown += 1;
        stateB.TimesShown += 1;

        switch (result)
        {
            case VoteResult.BanA:
                stateA.IsBanned = true;
                break;
            case VoteResult.BanB:
                stateB.IsBanned = true;
                break;
            case VoteResult.BanBoth:
                stateA.IsBanned = true;
                stateB.IsBanned = true;
                break;
            case VoteResult.PreferA:
            case VoteResult.PreferB:
            case VoteResult.LikeBoth:
                var scoreA = result switch
                {
                    VoteResult.PreferA => 1.0,
                    VoteResult.PreferB => 0.0,
                    _ => 0.5
                };
                var update = EloCalculator.Apply(stateA.EloScore, stateB.EloScore, kA, kB, scoreA);
                stateA.EloScore = update.NewEloA;
                stateB.EloScore = update.NewEloB;
                break;
        }

        db.Votes.Add(new Vote
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ItemA = itemA,
            ItemB = itemB,
            Result = result,
            PresentedAt = DateTimeOffset.UtcNow
        });

        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<ItemState> GetOrCreate(Guid sessionId, string item, CancellationToken ct)
    {
        var s = await db.ItemStates.FirstOrDefaultAsync(x => x.SessionId == sessionId && x.Item == item, ct);
        if (s is not null) return s;
        s = new ItemState { SessionId = sessionId, Item = item };
        db.ItemStates.Add(s);
        return s;
    }
}
