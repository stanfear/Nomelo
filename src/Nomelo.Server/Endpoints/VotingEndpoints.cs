using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Auth;
using Nomelo.Server.Data;
using Nomelo.Server.Data.Entities;
using Nomelo.Server.Lists;
using Nomelo.Server.Scoring;
using Nomelo.Server.Voting;
using Nomelo.Shared.Dtos;

namespace Nomelo.Server.Endpoints;

public static class VotingEndpoints
{
    public static IEndpointRouteBuilder MapVotingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions/{id:guid}").RequireAuthorization();

        group.MapGet("/next-pair", async (
            Guid id, AppDbContext db, NextPairService service,
            ICurrentUser currentUser, CancellationToken ct) =>
        {
            if (!await OwnsSession(db, id, currentUser.UserId, ct)) return Results.NotFound();
            var pair = await service.Next(id, ct);
            return pair is null ? Results.NoContent() : Results.Ok(pair);
        });

        group.MapPost("/votes", async (
            Guid id, VoteRequest req, AppDbContext db, VoteProcessor processor,
            ICurrentUser currentUser, CancellationToken ct) =>
        {
            if (!await OwnsSession(db, id, currentUser.UserId, ct)) return Results.NotFound();
            if (!TryParseResult(req.Result, out var result))
                return Results.BadRequest(new { error = "invalid result" });
            if (string.IsNullOrEmpty(req.ItemA) || string.IsNullOrEmpty(req.ItemB) || req.ItemA == req.ItemB)
                return Results.BadRequest(new { error = "itemA and itemB must be distinct non-empty values" });

            await processor.Apply(id, req.ItemA, req.ItemB, result, ct);
            return Results.NoContent();
        });

        group.MapGet("/results", async (
            Guid id, AppDbContext db, ListCache cache,
            ICurrentUser currentUser, CancellationToken ct) =>
        {
            if (!await OwnsSession(db, id, currentUser.UserId, ct)) return Results.NotFound();
            return Results.Ok(await BuildResults(id, db, cache, ct));
        });

        return app;
    }

    internal static async Task<ResultsDto> BuildResults(Guid sessionId, AppDbContext db, ListCache cache, CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, ct);
        if (!cache.TryGet(session.ListId, out var list) || list is null)
            throw new InvalidOperationException($"list {session.ListId} not loaded");

        var listMeta = await db.Lists.AsNoTracking().FirstAsync(l => l.Id == session.ListId, ct);

        var states = await db.ItemStates.AsNoTracking()
            .Where(s => s.SessionId == sessionId)
            .ToDictionaryAsync(s => s.Item, s => (s.EloScore, s.TimesShown, s.IsBanned), ct);

        var (ranked, banned) = ResultsBuilder.Build(list, states);

        var recentVotes = await db.Votes.AsNoTracking()
            .Where(v => v.SessionId == sessionId)
            .OrderByDescending(v => v.PresentedAt)
            .Take(StabilityCounter.StabilityThreshold)
            .Select(v => new { v.ItemA, v.ItemB, v.Result })
            .ToListAsync(ct);

        var upsetFlags = recentVotes.Select(v =>
        {
            var ea = states.TryGetValue(v.ItemA, out var sa) ? sa.EloScore : 1000.0;
            var eb = states.TryGetValue(v.ItemB, out var sb) ? sb.EloScore : 1000.0;
            return UpsetDetector.IsUpset(v.Result, ea, eb);
        }).ToList();

        var stable = StabilityCounter.ConsecutiveNonUpsets(upsetFlags) >= StabilityCounter.StabilityThreshold;
        var voteCount = await db.Votes.AsNoTracking().CountAsync(v => v.SessionId == sessionId, ct);

        return new ResultsDto(sessionId, listMeta.Id, listMeta.Name, voteCount, stable, ranked, banned);
    }

    private static async Task<bool> OwnsSession(AppDbContext db, Guid id, string userId, CancellationToken ct)
        => await db.Sessions.AsNoTracking().AnyAsync(s => s.Id == id && s.UserId == userId, ct);

    private static bool TryParseResult(string raw, out VoteResult result)
    {
        result = default;
        switch (raw)
        {
            case "prefer_a": result = VoteResult.PreferA; return true;
            case "prefer_b": result = VoteResult.PreferB; return true;
            case "ban_a": result = VoteResult.BanA; return true;
            case "ban_b": result = VoteResult.BanB; return true;
            case "ban_both": result = VoteResult.BanBoth; return true;
            case "like_both": result = VoteResult.LikeBoth; return true;
            default: return false;
        }
    }
}
