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
            new ListFileItem("Alice", Array.Empty<string>(), null, null, null, null),
            new ListFileItem("Bob", Array.Empty<string>(), null, null, null, null),
            new ListFileItem("Carol", Array.Empty<string>(), null, null, null, null)
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

    [Theory]
    [InlineData(VoteResult.PreferA)]
    [InlineData(VoteResult.PreferB)]
    [InlineData(VoteResult.LikeBoth)]
    [InlineData(VoteResult.BanA)]
    [InlineData(VoteResult.BanB)]
    [InlineData(VoteResult.BanBoth)]
    public async Task UndoLast_restores_state_after_single_vote(VoteResult result)
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Seed with non-default state so we verify recovery is exact.
        db.ItemStates.AddRange(
            new ItemState { SessionId = sid, Item = "Alice", EloScore = 1234.5, TimesShown = 7 },
            new ItemState { SessionId = sid, Item = "Bob", EloScore = 987.25, TimesShown = 3 });
        await db.SaveChangesAsync();

        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();
        await processor.Apply(sid, "Alice", "Bob", result);

        var undone = await processor.UndoLast(sid);
        undone.Should().BeTrue();

        using var scope2 = _sp.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var alice = await db2.ItemStates.FindAsync(sid, "Alice");
        var bob = await db2.ItemStates.FindAsync(sid, "Bob");

        alice!.EloScore.Should().BeApproximately(1234.5, 1e-6);
        bob!.EloScore.Should().BeApproximately(987.25, 1e-6);
        alice.TimesShown.Should().Be(7);
        bob.TimesShown.Should().Be(3);
        alice.IsBanned.Should().BeFalse();
        bob.IsBanned.Should().BeFalse();

        var votes = await db2.Votes.Where(v => v.SessionId == sid).ToListAsync();
        votes.Should().BeEmpty();
    }

    [Fact]
    public async Task UndoLast_returns_false_when_no_votes()
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();
        (await processor.UndoLast(sid)).Should().BeFalse();
    }

    [Fact]
    public async Task UndoLast_only_removes_the_most_recent_vote_in_a_chain()
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();

        await processor.Apply(sid, "Alice", "Bob", VoteResult.PreferA);
        // Ensure deterministic chronological ordering across the chain even on
        // systems with low-resolution clocks.
        await Task.Delay(5);
        await processor.Apply(sid, "Alice", "Carol", VoteResult.PreferB);
        await Task.Delay(5);
        await processor.Apply(sid, "Bob", "Carol", VoteResult.BanA);

        using var snapshotScope = _sp.CreateScope();
        var snapDb = snapshotScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var aliceAfter2 = (await snapDb.ItemStates.FindAsync(sid, "Alice"))!;
        var bobAfter2 = (await snapDb.ItemStates.FindAsync(sid, "Bob"))!;
        var carolAfter2 = (await snapDb.ItemStates.FindAsync(sid, "Carol"))!;
        var aliceEloPre3 = aliceAfter2.EloScore;
        var bobEloPre3 = bobAfter2.EloScore;
        var carolEloPre3 = carolAfter2.EloScore;
        var bobShownPre3 = bobAfter2.TimesShown;
        var carolShownPre3 = carolAfter2.TimesShown;

        // Third vote is BanA → Bob banned. UndoLast must unban Bob and leave
        // Alice/Carol state untouched relative to the post-2nd-vote snapshot.
        await processor.UndoLast(sid);

        using var scope2 = _sp.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var alice = (await db2.ItemStates.FindAsync(sid, "Alice"))!;
        var bob = (await db2.ItemStates.FindAsync(sid, "Bob"))!;
        var carol = (await db2.ItemStates.FindAsync(sid, "Carol"))!;

        alice.EloScore.Should().BeApproximately(aliceEloPre3, 1e-6);
        bob.EloScore.Should().BeApproximately(bobEloPre3, 1e-6);
        carol.EloScore.Should().BeApproximately(carolEloPre3, 1e-6);
        bob.IsBanned.Should().BeFalse();
        bob.TimesShown.Should().Be(bobShownPre3 - 1);
        carol.TimesShown.Should().Be(carolShownPre3 - 1);

        var votes = await db2.Votes.Where(v => v.SessionId == sid).CountAsync();
        votes.Should().Be(2);
    }

    [Fact]
    public async Task UndoLast_then_redo_yields_identical_state()
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();

        await processor.Apply(sid, "Alice", "Bob", VoteResult.PreferA);
        await Task.Delay(5);
        await processor.Apply(sid, "Alice", "Bob", VoteResult.PreferB);

        using var s1 = _sp.CreateScope();
        var db1 = s1.ServiceProvider.GetRequiredService<AppDbContext>();
        var aliceBefore = (await db1.ItemStates.FindAsync(sid, "Alice"))!.EloScore;
        var bobBefore = (await db1.ItemStates.FindAsync(sid, "Bob"))!.EloScore;

        await processor.UndoLast(sid);
        await Task.Delay(5);
        await processor.Apply(sid, "Alice", "Bob", VoteResult.PreferB);

        using var s2 = _sp.CreateScope();
        var db2 = s2.ServiceProvider.GetRequiredService<AppDbContext>();
        var aliceAfter = (await db2.ItemStates.FindAsync(sid, "Alice"))!.EloScore;
        var bobAfter = (await db2.ItemStates.FindAsync(sid, "Bob"))!.EloScore;

        aliceAfter.Should().BeApproximately(aliceBefore, 1e-6);
        bobAfter.Should().BeApproximately(bobBefore, 1e-6);
    }

    [Theory]
    [InlineData(0, 0)]   // K=48, K=48
    [InlineData(0, 5)]   // K=48, K=32
    [InlineData(0, 15)]  // K=48, K=16
    [InlineData(5, 15)]  // K=32, K=16
    [InlineData(15, 15)] // K=16, K=16
    [InlineData(15, 0)]  // K=16, K=48 (mirror)
    public async Task UndoLast_restores_exact_elos_across_K_bracket_combinations(
        int timesShownA, int timesShownB)
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ItemStates.AddRange(
            new ItemState { SessionId = sid, Item = "Alice", EloScore = 1100, TimesShown = timesShownA },
            new ItemState { SessionId = sid, Item = "Bob", EloScore = 950, TimesShown = timesShownB });
        await db.SaveChangesAsync();

        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();
        await processor.Apply(sid, "Alice", "Bob", VoteResult.PreferA);
        await processor.UndoLast(sid);

        using var scope2 = _sp.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db2.ItemStates.FindAsync(sid, "Alice"))!.EloScore.Should().BeApproximately(1100, 1e-6);
        (await db2.ItemStates.FindAsync(sid, "Bob"))!.EloScore.Should().BeApproximately(950, 1e-6);
    }

    public static IEnumerable<object[]> MultiVoteSequences()
    {
        // Short mixed sequences exercising every result type and a variety of
        // pair shapes (same/different operands). Each sequence is described as
        // a list of (itemA, itemB, result) tuples.
        yield return new object[] { new[]
        {
            ("Alice", "Bob", VoteResult.PreferA),
            ("Alice", "Bob", VoteResult.PreferB),
            ("Alice", "Bob", VoteResult.LikeBoth),
        }};
        yield return new object[] { new[]
        {
            ("Alice", "Bob", VoteResult.PreferA),
            ("Bob", "Carol", VoteResult.PreferB),
            ("Alice", "Carol", VoteResult.LikeBoth),
            ("Alice", "Bob", VoteResult.PreferA),
        }};
        yield return new object[] { new[]
        {
            ("Alice", "Bob", VoteResult.BanA),
            ("Bob", "Carol", VoteResult.BanB),
            ("Alice", "Carol", VoteResult.BanBoth),
        }};
        yield return new object[] { new[]
        {
            ("Alice", "Bob", VoteResult.PreferA),
            ("Alice", "Carol", VoteResult.LikeBoth),
            ("Bob", "Carol", VoteResult.PreferB),
            ("Alice", "Bob", VoteResult.BanB),
            ("Alice", "Carol", VoteResult.PreferA),
            ("Bob", "Carol", VoteResult.BanA),
        }};
        // Long sequence pushing several items past each KFactor bracket
        // boundary (so different votes in the same chain use different K).
        yield return new object[] { new[]
        {
            ("Alice", "Bob", VoteResult.PreferA),   // shown 1/1
            ("Alice", "Bob", VoteResult.PreferB),   // shown 2/2
            ("Alice", "Carol", VoteResult.PreferA), // Alice 3, Carol 1
            ("Alice", "Bob", VoteResult.LikeBoth),  // Alice 4, Bob 3
            ("Alice", "Bob", VoteResult.PreferA),   // Alice 5 → K crosses 48→32
            ("Bob", "Carol", VoteResult.PreferB),   // Bob 4, Carol 2
            ("Alice", "Carol", VoteResult.LikeBoth),// Alice 6, Carol 3
            ("Alice", "Bob", VoteResult.PreferB),   // Alice 7, Bob 5 → Bob K crosses
        }};
    }

    [Theory]
    [MemberData(nameof(MultiVoteSequences))]
    public async Task Chain_of_votes_followed_by_full_undo_restores_initial_state(
        (string ItemA, string ItemB, VoteResult Result)[] sequence)
    {
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();

        // No initial ItemState rows → undoing must remove the rows or leave
        // them back at the default (1000, 0, false). Apply seeds them on the
        // first vote, so after a complete undo chain we expect default state.

        foreach (var v in sequence)
        {
            await processor.Apply(sid, v.ItemA, v.ItemB, v.Result);
            // Small delay so PresentedAt remains strictly increasing even on
            // coarse-resolution clocks.
            await Task.Delay(2);
        }

        // Undo every vote in reverse order.
        for (var i = 0; i < sequence.Length; i++)
        {
            (await processor.UndoLast(sid)).Should().BeTrue(
                $"undo step {i + 1}/{sequence.Length} must succeed");
        }

        using var verifyScope = _sp.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var states = await db.ItemStates
            .Where(s => s.SessionId == sid)
            .ToListAsync();

        foreach (var s in states)
        {
            s.EloScore.Should().BeApproximately(1000.0, 1e-6,
                $"Elo of {s.Item} must be back to default after full undo");
            s.TimesShown.Should().Be(0,
                $"TimesShown of {s.Item} must be back to zero after full undo");
            s.IsBanned.Should().BeFalse(
                $"{s.Item} must not be banned after full undo");
        }

        var voteCount = await db.Votes.CountAsync(v => v.SessionId == sid);
        voteCount.Should().Be(0);
    }

    [Fact]
    public async Task Randomized_long_chain_full_undo_returns_to_initial_state()
    {
        // Deterministic pseudo-random sequence — fixed seed so failures are
        // reproducible. Mixes every result type across three items, crossing
        // every KFactor bracket along the way.
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();

        var items = new[] { "Alice", "Bob", "Carol" };
        var results = Enum.GetValues<VoteResult>();
        var rng = new Random(0xC0FFEE);

        const int chainLength = 40;
        for (var i = 0; i < chainLength; i++)
        {
            int a = rng.Next(items.Length);
            int b;
            do { b = rng.Next(items.Length); } while (b == a);
            var r = results[rng.Next(results.Length)];
            await processor.Apply(sid, items[a], items[b], r);
            await Task.Delay(2);
        }

        for (var i = 0; i < chainLength; i++)
        {
            (await processor.UndoLast(sid)).Should().BeTrue();
        }

        using var verifyScope = _sp.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var states = await db.ItemStates
            .Where(s => s.SessionId == sid)
            .ToListAsync();

        foreach (var s in states)
        {
            s.EloScore.Should().BeApproximately(1000.0, 1e-6);
            s.TimesShown.Should().Be(0);
            s.IsBanned.Should().BeFalse();
        }
        (await db.Votes.CountAsync(v => v.SessionId == sid)).Should().Be(0);
    }

    [Fact]
    public async Task Partial_undo_mid_chain_then_continue_then_full_undo_returns_to_initial_state()
    {
        // Stress test: apply 5 votes, undo 2, apply 3 more, undo all 6 remaining.
        // Final state must still match initial.
        var sid = await CreateSession();
        using var scope = _sp.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<VoteProcessor>();

        async Task ApplyAndPause(string a, string b, VoteResult r)
        {
            await processor.Apply(sid, a, b, r);
            await Task.Delay(2);
        }

        await ApplyAndPause("Alice", "Bob", VoteResult.PreferA);
        await ApplyAndPause("Bob", "Carol", VoteResult.LikeBoth);
        await ApplyAndPause("Alice", "Carol", VoteResult.PreferB);
        await ApplyAndPause("Alice", "Bob", VoteResult.BanB);
        await ApplyAndPause("Alice", "Carol", VoteResult.PreferA);

        // Undo two
        await processor.UndoLast(sid);
        await processor.UndoLast(sid);

        // Apply three more (different shapes)
        await ApplyAndPause("Bob", "Carol", VoteResult.PreferA);
        await ApplyAndPause("Alice", "Carol", VoteResult.LikeBoth);
        await ApplyAndPause("Alice", "Bob", VoteResult.BanBoth);

        // Final undo of everything remaining (3 + 3 = 6 votes left).
        var remaining = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Votes.CountAsync(v => v.SessionId == sid);
        remaining.Should().Be(6);

        for (var i = 0; i < remaining; i++)
            (await processor.UndoLast(sid)).Should().BeTrue();

        using var verifyScope = _sp.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var states = await db.ItemStates
            .Where(s => s.SessionId == sid)
            .ToListAsync();

        foreach (var s in states)
        {
            s.EloScore.Should().BeApproximately(1000.0, 1e-6);
            s.TimesShown.Should().Be(0);
            s.IsBanned.Should().BeFalse();
        }
        (await db.Votes.CountAsync(v => v.SessionId == sid)).Should().Be(0);
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

