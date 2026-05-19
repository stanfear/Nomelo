using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nomelo.Server.Configuration;
using Nomelo.Server.Data;
using Nomelo.Server.Data.Entities;

namespace Nomelo.Server.Lists;

public class ListRegistrarHostedService(
    IServiceProvider services,
    IOptions<ListsOptions> options,
    ILogger<ListRegistrarHostedService> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var dir = options.Value.Directory;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<ListDirectoryScanner>();

        var results = scanner.Scan(dir).ToList();
        var seenIds = new HashSet<string>();
        var now = DateTimeOffset.UtcNow;

        foreach (var r in results)
        {
            if (r.Error is not null)
            {
                log.LogWarning("Skipping list file {Path}: {Error}", r.Path, r.Error);
                continue;
            }

            var lf = r.List!;
            seenIds.Add(lf.Id);

            var existing = await db.Lists.FirstOrDefaultAsync(l => l.Id == lf.Id, ct);
            if (existing is null)
            {
                db.Lists.Add(new NameList
                {
                    Id = lf.Id,
                    Name = lf.Name,
                    FilePath = r.Path,
                    ItemCount = lf.Items.Count,
                    LoadedAt = now
                });
            }
            else
            {
                existing.Name = lf.Name;
                existing.FilePath = r.Path;
                existing.ItemCount = lf.Items.Count;
                existing.LoadedAt = now;
            }
        }

        var staleIds = await db.Lists
            .Where(l => !seenIds.Contains(l.Id))
            .Select(l => l.Id)
            .ToListAsync(ct);

        foreach (var staleId in staleIds)
        {
            var hasSessions = await db.Sessions.AnyAsync(s => s.ListId == staleId, ct);
            if (hasSessions)
            {
                log.LogWarning("List {ListId} disappeared from disk but has existing sessions; keeping the row", staleId);
                continue;
            }
            var toRemove = await db.Lists.FirstAsync(l => l.Id == staleId, ct);
            db.Lists.Remove(toRemove);
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Registered {Count} lists from {Dir}", seenIds.Count, dir);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
