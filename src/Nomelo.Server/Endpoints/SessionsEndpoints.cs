using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Auth;
using Nomelo.Server.Data;
using Nomelo.Server.Data.Entities;
using Nomelo.Server.Scoring;
using Nomelo.Shared.Dtos;

namespace Nomelo.Server.Endpoints;

internal sealed class SessionsEndpointsLog;

public static class SessionsEndpoints
{
    public static IEndpointRouteBuilder MapSessionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions").RequireAuthorization();

        group.MapGet("", async (AppDbContext db, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var userId = currentUser.UserId;
            var sessions = await db.Sessions
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .Join(db.Lists.AsNoTracking(), s => s.ListId, l => l.Id,
                    (s, l) => new { s, ListName = l.Name })
                .ToListAsync(ct);

            var ids = sessions.Select(x => x.s.Id).ToList();
            var counts = await db.Votes
                .AsNoTracking()
                .Where(v => ids.Contains(v.SessionId))
                .GroupBy(v => v.SessionId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

            var dtos = sessions.Select(x => new SessionDto(
                x.s.Id, x.s.ListId, x.ListName, x.s.ConfidenceThreshold,
                x.s.CreatedAt, x.s.UpdatedAt, x.s.ShareToken,
                counts.GetValueOrDefault(x.s.Id, 0),
                false)).ToList();

            return Results.Ok(dtos);
        });

        group.MapPost("", async (
            CreateSessionRequest req,
            AppDbContext db,
            ICurrentUser currentUser,
            ILogger<SessionsEndpointsLog> logger,
            CancellationToken ct) =>
        {
            if (req.ConfidenceThreshold < 1 || req.ConfidenceThreshold > 10)
                return Results.BadRequest(new { error = "confidenceThreshold must be between 1 and 10" });

            var list = await db.Lists
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == req.ListId, ct);
            if (list is null)
            {
                logger.LogWarning("Create session rejected: list {ListId} not found", req.ListId);
                return Results.NotFound(new { error = "list not found" });
            }

            var userId = currentUser.UserId;
            var now = DateTimeOffset.UtcNow;
            var session = new VotingSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ListId = list.Id,
                ConfidenceThreshold = req.ConfidenceThreshold,
                CreatedAt = now,
                UpdatedAt = now,
                ShareToken = GenerateShareToken()
            };
            db.Sessions.Add(session);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Session {SessionId} created for user {UserId} on list {ListId}",
                session.Id, userId, list.Id);

            var dto = new SessionDto(session.Id, list.Id, list.Name,
                session.ConfidenceThreshold, session.CreatedAt, session.UpdatedAt,
                session.ShareToken, 0, false);
            return Results.Created($"/api/sessions/{session.Id}", dto);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var userId = currentUser.UserId;
            var session = await db.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
            if (session is null) return Results.NotFound();

            var list = await db.Lists
                .AsNoTracking()
                .FirstAsync(l => l.Id == session.ListId, ct);
            var voteCount = await db.Votes
                .AsNoTracking()
                .CountAsync(v => v.SessionId == id, ct);

            var states = await db.ItemStates.AsNoTracking()
                .Where(s => s.SessionId == id)
                .ToDictionaryAsync(s => s.Item, s => (s.EloScore, s.TimesShown, s.IsBanned), ct);
            var recentVotes = await db.Votes.AsNoTracking()
                .Where(v => v.SessionId == id)
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

            return Results.Ok(new SessionDto(session.Id, list.Id, list.Name,
                session.ConfidenceThreshold, session.CreatedAt, session.UpdatedAt,
                session.ShareToken, voteCount, stable));
        });

        return app;
    }

    private static string GenerateShareToken()
    {
        var bytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
