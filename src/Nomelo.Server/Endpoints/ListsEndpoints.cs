using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Data;
using Nomelo.Shared.Dtos;

namespace Nomelo.Server.Endpoints;

public static class ListsEndpoints
{
    public static IEndpointRouteBuilder MapListsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/lists", async (AppDbContext db) =>
        {
            var lists = await db.Lists
                .OrderBy(l => l.Name)
                .Select(l => new ListDto(l.Id, l.Name, l.ItemCount))
                .ToListAsync();
            return Results.Ok(lists);
        }).RequireAuthorization();

        return app;
    }
}
