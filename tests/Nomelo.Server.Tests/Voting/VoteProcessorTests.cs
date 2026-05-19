using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nomelo.Server.Data;
using Nomelo.Server.Data.Entities;
using Nomelo.Server.Lists;
using Nomelo.Server.Tests.Infrastructure;
using Nomelo.Server.Voting;
using Xunit;

namespace Nomelo.Server.Tests.Voting;

[Collection("postgres")]
public class VoteProcessorTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private ServiceProvider _sp = default!;

    public VoteProcessorTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_pg.ConnectionString));
        services.AddSingleton<ListCache>();
        services.AddScoped<VoteProcessor>();
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var listId = $"t-{Guid.NewGuid():N}";
        await db.Lists.AddAsync(new NameList
        {
            Id = listId, Name = "T", FilePath = "/x", ItemCount = 3, LoadedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        _listId = listId;

        var cache = _sp.GetRequiredService<ListCache>();
        cache.Set(new ListFile(listId, "T", new[]
        {
            new ListFileItem("Alice", Array.Empty<string>(), null),
            new ListFileItem("Bob", Array.Empty<string>(), null),
            new ListFileItem("Carol", Array.Empty<string>(), null)
        }));
    }

    private string _listId = "";

    public Task DisposeAsync() { _sp.Dispose(); return Task.CompletedTask; }

    private async Task<Guid> CreateSession()
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = new VotingSession
        {
            Id = Guid.NewGuid(), UserId = "u", ListId = _listId,
            ConfidenceThreshold = 3, CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Sessions.Add(s);
        await db.SaveChangesAsync();
        return s.Id;
    }

    [Fact]
    public async Task PreferA_creates_item_states_and_updates_elos()
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();

        await processor.Apply(sid, "Alice", "Bob", VoteResult.PreferA);

        var alice = await db.ItemStates.FindAsync(sid, "Alice");
        var bob = await db.ItemStates.FindAsync(sid, "Bob");
        alice!.EloScore.Should().BeGreaterThan(1000);
        bob!.EloScore.Should().BeLessThan(1000);
        alice.TimesShown.Should().Be(1);
        bob.TimesShown.Should().Be(1);

        var votes = await db.Votes.Where(v => v.SessionId == sid).ToListAsync();
        votes.Should().ContainSingle();
    }

    [Fact]
    public async Task BanA_marks_only_A_as_banned_and_does_not_change_elos()
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();

        await processor.Apply(sid, "Alice", "Bob", VoteResult.BanA);

        var alice = await db.ItemStates.FindAsync(sid, "Alice");
        var bob = await db.ItemStates.FindAsync(sid, "Bob");
        alice!.IsBanned.Should().BeTrue();
        alice.EloScore.Should().Be(1000);
        bob!.IsBanned.Should().BeFalse();
        bob.EloScore.Should().Be(1000);
        alice.TimesShown.Should().Be(1);
        bob.TimesShown.Should().Be(1);
    }

    [Fact]
    public async Task BanBoth_marks_both_as_banned()
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();

        await processor.Apply(sid, "Alice", "Bob", VoteResult.BanBoth);

        (await db.ItemStates.FindAsync(sid, "Alice"))!.IsBanned.Should().BeTrue();
        (await db.ItemStates.FindAsync(sid, "Bob"))!.IsBanned.Should().BeTrue();
    }

    [Fact]
    public async Task LikeBoth_pulls_elos_toward_each_other()
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ItemStates.AddRange(
            new ItemState { SessionId = sid, Item = "Alice", EloScore = 1200, TimesShown = 5 },
            new ItemState { SessionId = sid, Item = "Bob", EloScore = 1000, TimesShown = 5 });
        await db.SaveChangesAsync();

        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();
        await processor.Apply(sid, "Alice", "Bob", VoteResult.LikeBoth);

        var alice = await db.ItemStates.FindAsync(sid, "Alice");
        var bob = await db.ItemStates.FindAsync(sid, "Bob");
        alice!.EloScore.Should().BeLessThan(1200);
        bob!.EloScore.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task Updates_session_updated_at()
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = (await db.Sessions.FindAsync(sid))!.UpdatedAt;

        await Task.Delay(20);
        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();
        await processor.Apply(sid, "Alice", "Bob", VoteResult.PreferA);

        using var scope2 = _sp.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var after = (await db2.Sessions.FindAsync(sid))!.UpdatedAt;
        after.Should().BeAfter(before);
    }
}
