using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Data;
using Nomelo.Server.Lists;

namespace Nomelo.Server.Endpoints;

public static class ShareEndpoints
{
    public static IEndpointRouteBuilder MapShareEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/share/{token}", async (
            string token, AppDbContext db, ListCache cache, CancellationToken ct) =>
        {
            var session = await db.Sessions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShareToken == token, ct);
            if (session is null) return Results.NotFound();
            return Results.Ok(await VotingEndpoints.BuildResults(session.Id, db, cache, ct));
        }).AllowAnonymous();

        return app;
    }
}
