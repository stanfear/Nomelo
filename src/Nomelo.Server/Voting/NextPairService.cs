using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Data;
using Nomelo.Server.Lists;
using Nomelo.Server.Scoring;
using Nomelo.Shared.Dtos;

namespace Nomelo.Server.Voting;

public class NextPairService(AppDbContext db, ListCache cache)
{
    private const int AntiRepeatWindow = 20;

    public async Task<PairDto?> Next(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.Sessions.AsNoTracking()
                          .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
                      ?? throw new InvalidOperationException($"session {sessionId} not found");

        if (!cache.TryGet(session.ListId, out var list) || list is null)
            throw new InvalidOperationException($"list {session.ListId} not loaded");

        var states = await db.ItemStates.AsNoTracking()
            .Where(s => s.SessionId == sessionId)
            .ToDictionaryAsync(s => s.Item, ct);

        var snapshots = list.Items.Select(i =>
        {
            states.TryGetValue(i.Value, out var st);
            return new ItemSnapshot(
                i.Value,
                st?.EloScore ?? 1000.0,
                st?.TimesShown ?? 0,
                st?.IsBanned ?? false);
        }).ToList();

        var recent = await db.Votes.AsNoTracking()
            .Where(v => v.SessionId == sessionId)
            .OrderByDescending(v => v.PresentedAt)
            .Take(AntiRepeatWindow)
            .Select(v => new { v.ItemA, v.ItemB })
            .ToListAsync(ct);
        var recentPairs = recent.Select(r => (r.ItemA, r.ItemB)).ToList();

        var picked = PairSelector.Pick(snapshots, recentPairs, session.ConfidenceThreshold, Random.Shared);
        if (picked is null) return null;

        var byValue = list.Items.ToDictionary(i => i.Value);
        var a = byValue[picked.A];
        var b = byValue[picked.B];

        return new PairDto(
            new PairItemDto(a.Value, a.Variants, a.Description),
            new PairItemDto(b.Value, b.Variants, b.Description));
    }
}
