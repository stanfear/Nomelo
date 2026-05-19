using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Data;
using Nomelo.Server.Data.Entities;
using Nomelo.Shared.Dtos;

namespace Nomelo.Server.Endpoints;

public static class SessionsEndpoints
{
    public static IEndpointRouteBuilder MapSessionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions").RequireAuthorization();

        group.MapGet("", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = RequireUserId(user);
            var sessions = await db.Sessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .Join(db.Lists, s => s.ListId, l => l.Id,
                    (s, l) => new { s, ListName = l.Name })
                .ToListAsync();

            var ids = sessions.Select(x => x.s.Id).ToList();
            var counts = await db.Votes
                .Where(v => ids.Contains(v.SessionId))
                .GroupBy(v => v.SessionId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            var dtos = sessions.Select(x => new SessionDto(
                x.s.Id, x.s.ListId, x.ListName, x.s.ConfidenceThreshold,
                x.s.CreatedAt, x.s.UpdatedAt, x.s.ShareToken,
                counts.GetValueOrDefault(x.s.Id, 0))).ToList();

            return Results.Ok(dtos);
        });

        group.MapPost("", async (CreateSessionRequest req, AppDbContext db, ClaimsPrincipal user) =>
        {
            if (req.ConfidenceThreshold < 1 || req.ConfidenceThreshold > 10)
                return Results.BadRequest(new { error = "confidenceThreshold must be between 1 and 10" });

            var list = await db.Lists.FirstOrDefaultAsync(l => l.Id == req.ListId);
            if (list is null) return Results.NotFound(new { error = "list not found" });

            var userId = RequireUserId(user);
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
            await db.SaveChangesAsync();

            var dto = new SessionDto(session.Id, list.Id, list.Name,
                session.ConfidenceThreshold, session.CreatedAt, session.UpdatedAt,
                session.ShareToken, 0);
            return Results.Created($"/api/sessions/{session.Id}", dto);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = RequireUserId(user);
            var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
            if (session is null) return Results.NotFound();

            var list = await db.Lists.FirstAsync(l => l.Id == session.ListId);
            var voteCount = await db.Votes.CountAsync(v => v.SessionId == id);

            return Results.Ok(new SessionDto(session.Id, list.Id, list.Name,
                session.ConfidenceThreshold, session.CreatedAt, session.UpdatedAt,
                session.ShareToken, voteCount));
        });

        return app;
    }

    private static string RequireUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirst("sub")?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub))
            throw new InvalidOperationException("user has no sub claim");
        return sub;
    }

    private static string GenerateShareToken()
    {
        var bytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
