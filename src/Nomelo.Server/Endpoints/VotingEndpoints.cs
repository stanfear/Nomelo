using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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

        group.MapDelete("/votes/last", async (
            Guid id, AppDbContext db, VoteProcessor processor,
            ICurrentUser currentUser, CancellationToken ct) =>
        {
            if (!await OwnsSession(db, id, currentUser.UserId, ct)) return Results.NotFound();
            var undone = await processor.UndoLast(id, ct);
            return undone ? Results.NoContent() : Results.NotFound(new { error = "no votes to undo" });
        });

        group.MapPost("/items/bans", async (
            Guid id, BulkBanRequest req, AppDbContext db, ListCache cache,
            ICurrentUser currentUser, CancellationToken ct) =>
        {
            if (!await OwnsSession(db, id, currentUser.UserId, ct)) return Results.NotFound();
            if (req.Items is null || req.Items.Count == 0)
                return Results.BadRequest(new { error = "items must be a non-empty array" });

            var session = await db.Sessions.AsNoTracking()
                .FirstAsync(s => s.Id == id, ct);
            if (!cache.TryGet(session.ListId, out var list) || list is null)
                return Results.Problem($"list {session.ListId} not loaded");

            var listValues = list.Items.Select(i => i.Value).ToHashSet(StringComparer.Ordinal);
            // Deduplicate the input client-side, then validate every item
            // belongs to the session's list before any DB write.
            var requested = req.Items.Distinct(StringComparer.Ordinal).ToList();
            var unknown = requested.Where(v => !listValues.Contains(v)).ToList();
            if (unknown.Count > 0)
                return Results.BadRequest(new { error = "unknown items", items = unknown });

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var existing = await db.ItemStates
                .Where(s => s.SessionId == id && requested.Contains(s.Item))
                .ToListAsync(ct);
            var existingItems = existing.Select(s => s.Item).ToHashSet(StringComparer.Ordinal);

            foreach (var s in existing) s.IsBanned = true;

            foreach (var item in requested.Where(v => !existingItems.Contains(v)))
            {
                db.ItemStates.Add(new ItemState
                {
                    SessionId = id,
                    Item = item,
                    EloScore = 1000.0,
                    TimesShown = 0,
                    IsBanned = true
                });
            }

            var sessionRow = await db.Sessions.FirstAsync(s => s.Id == id, ct);
            sessionRow.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.NoContent();
        });

        group.MapPost("/items/unbans", async (
            Guid id, BulkBanRequest req, AppDbContext db,
            ICurrentUser currentUser, CancellationToken ct) =>
        {
            if (!await OwnsSession(db, id, currentUser.UserId, ct)) return Results.NotFound();
            if (req.Items is null || req.Items.Count == 0)
                return Results.BadRequest(new { error = "items must be a non-empty array" });

            var requested = req.Items.Distinct(StringComparer.Ordinal).ToList();

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Unban only touches rows that already exist — an item with no
            // ItemState row is implicitly unbanned, so there is nothing to do.
            // No list-membership validation is needed: rows that don't belong
            // to the session simply won't be found.
            var rows = await db.ItemStates
                .Where(s => s.SessionId == id && requested.Contains(s.Item))
                .ToListAsync(ct);
            foreach (var s in rows) s.IsBanned = false;

            var session = await db.Sessions.FirstAsync(s => s.Id == id, ct);
            session.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.NoContent();
        });

        group.MapGet("/results", async (
            Guid id, AppDbContext db, ListCache cache,
            ICurrentUser currentUser, CancellationToken ct) =>
        {
            if (!await OwnsSession(db, id, currentUser.UserId, ct)) return Results.NotFound();
            return Results.Ok(await BuildResults(id, db, cache, ct));
        });

        group.MapPost("/export-unbanned", async (
            Guid id, ExportUnbannedRequest req, AppDbContext db,
            ICurrentUser currentUser, CancellationToken ct) =>
        {
            if (!await OwnsSession(db, id, currentUser.UserId, ct)) return Results.NotFound();
            return await ExportUnbanned(id, req, db, ct);
        });

        return app;
    }

    private static readonly Regex SlugRegex = new("^[a-z0-9][a-z0-9_-]*$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions ExportJson = new() { WriteIndented = true };

    internal static async Task<IResult> ExportUnbanned(
        Guid sessionId, ExportUnbannedRequest req, AppDbContext db, CancellationToken ct)
    {
        var newId = req.NewId?.Trim() ?? "";
        var newName = req.NewName?.Trim() ?? "";
        if (newId.Length == 0 || newId.Length > 64 || !SlugRegex.IsMatch(newId))
            return Results.BadRequest(new { error = "id must be lowercase alphanumeric (- _ allowed), 1-64 chars" });
        if (newName.Length == 0 || newName.Length > 120)
            return Results.BadRequest(new { error = "name must be 1-120 chars" });

        var session = await db.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, ct);
        var listMeta = await db.Lists.AsNoTracking().FirstOrDefaultAsync(l => l.Id == session.ListId, ct);
        if (listMeta is null) return Results.NotFound(new { error = "source list not found" });
        if (string.IsNullOrEmpty(listMeta.FilePath) || !File.Exists(listMeta.FilePath))
            return Results.Problem("source list file missing on disk");

        // Collision with an already-registered list would silently overwrite
        // the source if the user later drops the file into lists/. Reject up
        // front so the failure is visible.
        if (await db.Lists.AsNoTracking().AnyAsync(l => l.Id == newId, ct))
            return Results.Conflict(new { error = $"a list with id '{newId}' already exists" });

        var banned = await db.ItemStates.AsNoTracking()
            .Where(s => s.SessionId == sessionId && s.IsBanned)
            .Select(s => s.Item)
            .ToListAsync(ct);
        var bannedSet = new HashSet<string>(banned, StringComparer.Ordinal);

        await using var stream = File.OpenRead(listMeta.FilePath);
        var node = (await JsonNode.ParseAsync(stream, cancellationToken: ct)) as JsonObject;
        if (node is null) return Results.Problem("source list JSON is not an object");

        node["id"] = newId;
        node["name"] = newName;

        var filtered = new JsonArray();
        if (node["items"] is JsonArray items)
        {
            foreach (var item in items)
            {
                if (item is JsonObject itemObj
                    && itemObj["value"]?.GetValue<string>() is { } value
                    && !bannedSet.Contains(value))
                {
                    filtered.Add(item.DeepClone());
                }
            }
        }
        if (filtered.Count == 0)
            return Results.BadRequest(new { error = "no items would remain after filtering" });
        node["items"] = filtered;

        var bytes = Encoding.UTF8.GetBytes(node.ToJsonString(ExportJson));
        return Results.File(bytes, "application/json", fileDownloadName: $"{newId}.json");
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

        return new ResultsDto(sessionId, listMeta.Id, listMeta.Name, session.Name, voteCount, stable, ranked, banned);
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
