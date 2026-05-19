using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Data;
using Nomelo.Shared.Dtos;

namespace Nomelo.Server.Endpoints;

public static class ListsEndpoints
{
    public static IEndpointRouteBuilder MapListsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/lists", async (AppDbContext db, CancellationToken ct) =>
        {
            var lists = await db.Lists
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .Select(l => new ListDto(l.Id, l.Name, l.ItemCount))
                .ToListAsync(ct);
            return Results.Ok(lists);
        }).RequireAuthorization();

        return app;
    }
}
