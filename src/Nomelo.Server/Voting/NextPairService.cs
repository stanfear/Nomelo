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

        // Deterministic seed = stable hash of (sessionId, voteCount). Refreshing
        // the page before voting always yields the same pair; once a vote is
        // recorded, voteCount increments and the next pair becomes a fresh draw.
        var voteCount = await db.Votes.AsNoTracking()
            .CountAsync(v => v.SessionId == sessionId, ct);
        var rng = new Random(SeedFor(sessionId, voteCount));

        var picked = PairSelector.Pick(snapshots, recentPairs, session.ConfidenceThreshold, rng);
        if (picked is null) return null;

        var byValue = list.Items.ToDictionary(i => i.Value);
        var a = byValue[picked.A];
        var b = byValue[picked.B];

        return new PairDto(
            new PairItemDto(a.Value, a.Variants, a.Description),
            new PairItemDto(b.Value, b.Variants, b.Description));
    }

    // Cross-process-stable seed combining the session GUID bytes with the vote
    // count. Avoids HashCode.Combine which is randomized per process.
    private static int SeedFor(Guid sessionId, int voteCount)
    {
        Span<byte> bytes = stackalloc byte[16];
        sessionId.TryWriteBytes(bytes);
        var part1 = BitConverter.ToInt32(bytes[..4]);
        var part2 = BitConverter.ToInt32(bytes[4..8]);
        var part3 = BitConverter.ToInt32(bytes[8..12]);
        var part4 = BitConverter.ToInt32(bytes[12..16]);
        return part1 ^ part2 ^ part3 ^ part4 ^ voteCount;
    }
}
