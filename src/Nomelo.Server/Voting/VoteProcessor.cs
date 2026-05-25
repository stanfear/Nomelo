using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Data;
using Nomelo.Server.Data.Entities;
using Nomelo.Server.Lists;
using Nomelo.Server.Scoring;

namespace Nomelo.Server.Voting;

public class VoteProcessor(AppDbContext db, ListCache cache)
{
    public async Task Apply(Guid sessionId, string itemA, string itemB, VoteResult result, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(itemA) || string.IsNullOrEmpty(itemB))
            throw new ArgumentException("itemA and itemB must be non-empty");
        if (itemA == itemB)
            throw new ArgumentException("itemA and itemB must be distinct");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
                      ?? throw new InvalidOperationException($"session {sessionId} not found");

        if (!cache.TryGet(session.ListId, out var list) || list is null)
            throw new InvalidOperationException($"list {session.ListId} not loaded");

        var validValues = list.Items.Select(i => i.Value).ToHashSet(StringComparer.Ordinal);
        if (!validValues.Contains(itemA))
            throw new ArgumentException($"item '{itemA}' does not belong to list '{session.ListId}'");
        if (!validValues.Contains(itemB))
            throw new ArgumentException($"item '{itemB}' does not belong to list '{session.ListId}'");

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

    public async Task<bool> UndoLast(Guid sessionId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
                      ?? throw new InvalidOperationException($"session {sessionId} not found");

        // PresentedAt + Id tiebreaker keeps ordering deterministic if two votes
        // happen to land in the same tick.
        var lastVote = await db.Votes
            .Where(v => v.SessionId == sessionId)
            .OrderByDescending(v => v.PresentedAt)
            .ThenByDescending(v => v.Id)
            .FirstOrDefaultAsync(ct);
        if (lastVote is null) return false;

        var stateA = await db.ItemStates.FirstAsync(
            s => s.SessionId == sessionId && s.Item == lastVote.ItemA, ct);
        var stateB = await db.ItemStates.FirstAsync(
            s => s.SessionId == sessionId && s.Item == lastVote.ItemB, ct);

        // KFactor in Apply was computed from TimesShown *before* incrementing.
        var kA = EloCalculator.KFactor(stateA.TimesShown - 1);
        var kB = EloCalculator.KFactor(stateB.TimesShown - 1);

        stateA.TimesShown -= 1;
        stateB.TimesShown -= 1;

        switch (lastVote.Result)
        {
            case VoteResult.BanA:
                stateA.IsBanned = false;
                break;
            case VoteResult.BanB:
                stateB.IsBanned = false;
                break;
            case VoteResult.BanBoth:
                stateA.IsBanned = false;
                stateB.IsBanned = false;
                break;
            case VoteResult.PreferA:
            case VoteResult.PreferB:
            case VoteResult.LikeBoth:
                var scoreA = lastVote.Result switch
                {
                    VoteResult.PreferA => 1.0,
                    VoteResult.PreferB => 0.0,
                    _ => 0.5
                };
                var prior = EloInverter.Invert(stateA.EloScore, stateB.EloScore, kA, kB, scoreA);
                stateA.EloScore = prior.EloABefore;
                stateB.EloScore = prior.EloBBefore;
                break;
        }

        db.Votes.Remove(lastVote);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
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
