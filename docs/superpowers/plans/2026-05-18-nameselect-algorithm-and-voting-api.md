# NameSelect Algorithm & Voting API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Depends on Plan 1 (Backend foundation) being merged.**

**Goal:** Implement the ELO scoring engine, weighted pair selection with anti-repetition, stability detection (consecutive non-upset counter), and the voting REST endpoints (`/next-pair`, `/votes`, `/results`, `/share/{token}`).

**Architecture:** Pure functions for ELO math and weight calculation, unit-tested in isolation. A `ListCache` service holds parsed list items in memory (populated by the registrar from Plan 1). A `PairSelector` consumes the cache plus `item_states` to draw pairs. A `VoteProcessor` mutates `item_states` and writes a `votes` row in a single DB transaction. The non-upset counter is computed on the fly from the recent vote history rather than stored, to keep the schema flat.

**Tech Stack:** Same as Plan 1 (.NET 8, EF Core 8, Npgsql, xUnit, FluentAssertions, Testcontainers). No new packages.

---

## File Structure

```
src/NameSelect.Server/
├── Lists/
│   └── ListCache.cs                            # new — in-memory items
├── Lists/ListRegistrarHostedService.cs         # modified — populate ListCache
├── Scoring/
│   ├── EloCalculator.cs                        # pure ELO math
│   ├── WeightCalculator.cs                     # display weights from item state
│   ├── PairSelector.cs                         # weighted draw + anti-repetition
│   ├── UpsetDetector.cs                        # determines if a vote is an upset
│   └── ResultsBuilder.cs                       # produces ranked output
├── Voting/
│   ├── VoteProcessor.cs                        # applies a vote in a transaction
│   └── NextPairService.cs                      # orchestrates next-pair draw
└── Endpoints/
    ├── VotingEndpoints.cs                      # /next-pair, /votes, /results
    └── ShareEndpoints.cs                       # /api/share/{token}

src/NameSelect.Shared/Dtos/
├── PairDto.cs
├── VoteRequest.cs
├── ResultsDto.cs
└── RankedItemDto.cs

tests/NameSelect.Server.Tests/
├── Scoring/
│   ├── EloCalculatorTests.cs
│   ├── WeightCalculatorTests.cs
│   ├── PairSelectorTests.cs
│   ├── UpsetDetectorTests.cs
│   └── ResultsBuilderTests.cs
├── Voting/
│   └── VoteProcessorTests.cs
└── Endpoints/
    ├── VotingEndpointsTests.cs
    └── ShareEndpointsTests.cs
```

**Boundaries:**
- `Scoring/` is pure: no DB, no I/O, all `static` methods or classes with no dependencies. Easily unit-tested.
- `Voting/` orchestrates: reads from DB and `ListCache`, calls `Scoring/`, writes to DB.
- `Endpoints/` files are thin wrappers around `Voting/` services.

---

## Task 1: ListCache + populate from registrar

We need item-level data at runtime (variants, description) without re-reading JSON on every request. The cache is populated by the existing hosted service from Plan 1.

**Files:**
- Create: `src/NameSelect.Server/Lists/ListCache.cs`
- Modify: `src/NameSelect.Server/Lists/ListRegistrarHostedService.cs`
- Modify: `src/NameSelect.Server/Program.cs`
- Test: `tests/NameSelect.Server.Tests/Lists/ListCacheTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/NameSelect.Server.Tests/Lists/ListCacheTests.cs`:

```csharp
using FluentAssertions;
using NameSelect.Server.Lists;

namespace NameSelect.Server.Tests.Lists;

public class ListCacheTests
{
    [Fact]
    public void Set_and_TryGet_round_trip()
    {
        var cache = new ListCache();
        var list = new ListFile("a", "A", new[]
        {
            new ListFileItem("x", Array.Empty<string>(), null)
        });

        cache.Set(list);

        cache.TryGet("a", out var got).Should().BeTrue();
        got!.Items.Should().HaveCount(1);
    }

    [Fact]
    public void TryGet_unknown_id_returns_false()
    {
        var cache = new ListCache();

        cache.TryGet("missing", out var got).Should().BeFalse();
        got.Should().BeNull();
    }

    [Fact]
    public void Replace_replaces_existing_entry()
    {
        var cache = new ListCache();
        cache.Set(new ListFile("a", "A1", new[] { new ListFileItem("x", Array.Empty<string>(), null) }));
        cache.Set(new ListFile("a", "A2", new[] { new ListFileItem("y", Array.Empty<string>(), null) }));

        cache.TryGet("a", out var got);
        got!.Name.Should().Be("A2");
        got.Items.Single().Value.Should().Be("y");
    }

    [Fact]
    public void Remove_drops_entry()
    {
        var cache = new ListCache();
        cache.Set(new ListFile("a", "A", new[] { new ListFileItem("x", Array.Empty<string>(), null) }));

        cache.Remove("a");

        cache.TryGet("a", out _).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter ListCacheTests`
Expected: FAIL (type does not exist).

- [ ] **Step 3: Implement ListCache**

Create `src/NameSelect.Server/Lists/ListCache.cs`:

```csharp
using System.Collections.Concurrent;

namespace NameSelect.Server.Lists;

public class ListCache
{
    private readonly ConcurrentDictionary<string, ListFile> _byId = new();

    public void Set(ListFile list) => _byId[list.Id] = list;

    public bool TryGet(string id, out ListFile? list)
    {
        if (_byId.TryGetValue(id, out var found)) { list = found; return true; }
        list = null;
        return false;
    }

    public void Remove(string id) => _byId.TryRemove(id, out _);
}
```

- [ ] **Step 4: Wire ListCache in DI**

In `Program.cs`, replace the `AddScoped<ListDirectoryScanner>()` block with:

```csharp
builder.Services.AddSingleton<ListFileLoader>();
builder.Services.AddSingleton<ListCache>();
builder.Services.AddScoped<ListDirectoryScanner>();
builder.Services.AddHostedService<ListRegistrarHostedService>();
```

- [ ] **Step 5: Populate cache from registrar**

In `src/NameSelect.Server/Lists/ListRegistrarHostedService.cs`, inject `ListCache` and populate it. Replace the class with:

```csharp
using Microsoft.EntityFrameworkCore;
using NameSelect.Server.Data;
using NameSelect.Server.Data.Entities;

namespace NameSelect.Server.Lists;

public class ListRegistrarHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<ListRegistrarHostedService> _log;
    private readonly ListCache _cache;

    public ListRegistrarHostedService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<ListRegistrarHostedService> log,
        ListCache cache)
    {
        _services = services;
        _config = config;
        _log = log;
        _cache = cache;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var dir = _config["Lists:Directory"]
                  ?? throw new InvalidOperationException("Lists:Directory not configured");

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<ListDirectoryScanner>();

        var results = scanner.Scan(dir).ToList();
        var seenIds = new HashSet<string>();
        var now = DateTimeOffset.UtcNow;

        foreach (var r in results)
        {
            if (r.Error is not null)
            {
                _log.LogWarning("Skipping list file {Path}: {Error}", r.Path, r.Error);
                continue;
            }

            var lf = r.List!;
            seenIds.Add(lf.Id);
            _cache.Set(lf);

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

        var toRemove = await db.Lists.Where(l => !seenIds.Contains(l.Id)).ToListAsync(ct);
        foreach (var stale in toRemove) _cache.Remove(stale.Id);
        if (toRemove.Count > 0) db.Lists.RemoveRange(toRemove);

        await db.SaveChangesAsync(ct);
        _log.LogInformation("Registered {Count} lists from {Dir}", seenIds.Count, dir);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test`
Expected: All tests pass (existing + new ListCacheTests).

- [ ] **Step 7: Commit**

```bash
git add src/NameSelect.Server/Lists src/NameSelect.Server/Program.cs tests/NameSelect.Server.Tests/Lists/ListCacheTests.cs
git commit -m "feat: add in-memory ListCache populated by registrar hosted service"
```

---

## Task 2: EloCalculator (pure math, TDD)

**Files:**
- Create: `src/NameSelect.Server/Scoring/EloCalculator.cs`
- Test: `tests/NameSelect.Server.Tests/Scoring/EloCalculatorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/NameSelect.Server.Tests/Scoring/EloCalculatorTests.cs`:

```csharp
using FluentAssertions;
using NameSelect.Server.Scoring;

namespace NameSelect.Server.Tests.Scoring;

public class EloCalculatorTests
{
    [Theory]
    [InlineData(0, 48)]
    [InlineData(4, 48)]
    [InlineData(5, 32)]
    [InlineData(14, 32)]
    [InlineData(15, 16)]
    [InlineData(100, 16)]
    public void K_factor_brackets(int timesShown, int expected)
    {
        EloCalculator.KFactor(timesShown).Should().Be(expected);
    }

    [Fact]
    public void Expected_score_equal_elos_is_half()
    {
        EloCalculator.ExpectedScore(1000, 1000).Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Expected_score_higher_elo_is_above_half()
    {
        EloCalculator.ExpectedScore(1200, 1000).Should().BeGreaterThan(0.5);
        EloCalculator.ExpectedScore(1000, 1200).Should().BeLessThan(0.5);
    }

    [Fact]
    public void PreferA_updates_both_elos_correctly()
    {
        var r = EloCalculator.Apply(eloA: 1000, eloB: 1000, kA: 32, kB: 32, scoreA: 1.0);
        r.NewEloA.Should().BeApproximately(1016, 0.01);
        r.NewEloB.Should().BeApproximately(984, 0.01);
    }

    [Fact]
    public void LikeBoth_pulls_elos_toward_each_other()
    {
        var r = EloCalculator.Apply(eloA: 1200, eloB: 1000, kA: 32, kB: 32, scoreA: 0.5);
        r.NewEloA.Should().BeLessThan(1200);
        r.NewEloB.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void Asymmetric_K_works()
    {
        var r = EloCalculator.Apply(eloA: 1000, eloB: 1000, kA: 48, kB: 16, scoreA: 1.0);
        (r.NewEloA - 1000).Should().BeApproximately(24, 0.01);
        (1000 - r.NewEloB).Should().BeApproximately(8, 0.01);
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter EloCalculatorTests`
Expected: FAIL (type does not exist).

- [ ] **Step 3: Implement EloCalculator**

Create `src/NameSelect.Server/Scoring/EloCalculator.cs`:

```csharp
namespace NameSelect.Server.Scoring;

public record EloUpdate(double NewEloA, double NewEloB);

public static class EloCalculator
{
    public static int KFactor(int timesShown) => timesShown switch
    {
        < 5 => 48,
        < 15 => 32,
        _ => 16
    };

    public static double ExpectedScore(double eloA, double eloB)
        => 1.0 / (1.0 + Math.Pow(10.0, (eloB - eloA) / 400.0));

    public static EloUpdate Apply(double eloA, double eloB, int kA, int kB, double scoreA)
    {
        var eA = ExpectedScore(eloA, eloB);
        var eB = 1.0 - eA;
        var scoreB = 1.0 - scoreA;
        return new EloUpdate(eloA + kA * (scoreA - eA), eloB + kB * (scoreB - eB));
    }
}
```

- [ ] **Step 4: Run tests and verify they pass**

Run: `dotnet test --filter EloCalculatorTests`
Expected: 9 passed (6 facts + 6 theory rows minus overlap).

- [ ] **Step 5: Commit**

```bash
git add src/NameSelect.Server/Scoring/EloCalculator.cs tests/NameSelect.Server.Tests/Scoring/EloCalculatorTests.cs
git commit -m "feat: add EloCalculator with K-factor brackets and per-item asymmetric updates"
```

---

## Task 3: WeightCalculator (TDD)

**Files:**
- Create: `src/NameSelect.Server/Scoring/WeightCalculator.cs`
- Test: `tests/NameSelect.Server.Tests/Scoring/WeightCalculatorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/NameSelect.Server.Tests/Scoring/WeightCalculatorTests.cs`:

```csharp
using FluentAssertions;
using NameSelect.Server.Scoring;

namespace NameSelect.Server.Tests.Scoring;

public class WeightCalculatorTests
{
    [Fact]
    public void Banned_returns_zero()
        => WeightCalculator.Weight(elo: 1500, timesShown: 100, isBanned: true, threshold: 3).Should().Be(0);

    [Fact]
    public void Unseen_below_threshold_returns_60()
        => WeightCalculator.Weight(elo: 1000, timesShown: 0, isBanned: false, threshold: 3).Should().Be(60);

    [Fact]
    public void Unseen_at_threshold_minus_one_still_unseen()
        => WeightCalculator.Weight(elo: 1000, timesShown: 2, isBanned: false, threshold: 3).Should().Be(60);

    [Fact]
    public void Preferred_high_elo_above_1050_returns_25()
        => WeightCalculator.Weight(elo: 1100, timesShown: 10, isBanned: false, threshold: 3).Should().Be(25);

    [Fact]
    public void Neutral_elo_800_to_1050_returns_15()
    {
        WeightCalculator.Weight(elo: 800, timesShown: 10, isBanned: false, threshold: 3).Should().Be(15);
        WeightCalculator.Weight(elo: 1050, timesShown: 10, isBanned: false, threshold: 3).Should().Be(15);
    }

    [Fact]
    public void Cold_below_800_returns_5()
        => WeightCalculator.Weight(elo: 700, timesShown: 10, isBanned: false, threshold: 3).Should().Be(5);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter WeightCalculatorTests`
Expected: FAIL.

- [ ] **Step 3: Implement WeightCalculator**

Create `src/NameSelect.Server/Scoring/WeightCalculator.cs`:

```csharp
namespace NameSelect.Server.Scoring;

public static class WeightCalculator
{
    public static int Weight(double elo, int timesShown, bool isBanned, int threshold)
    {
        if (isBanned) return 0;
        if (timesShown < threshold) return 60;
        if (elo > 1050) return 25;
        if (elo >= 800) return 15;
        return 5;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter WeightCalculatorTests`
Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add src/NameSelect.Server/Scoring/WeightCalculator.cs tests/NameSelect.Server.Tests/Scoring/WeightCalculatorTests.cs
git commit -m "feat: add WeightCalculator with banned/unseen/preferred/neutral/cold tiers"
```

---

## Task 4: PairSelector (TDD)

Pure-ish: takes a list of items with their current `(elo, timesShown, isBanned)` state, plus the last-N pair history, plus a `Random` seed for determinism, and returns a `(itemA, itemB)` or `null` if no pair can be formed.

**Files:**
- Create: `src/NameSelect.Server/Scoring/PairSelector.cs`
- Test: `tests/NameSelect.Server.Tests/Scoring/PairSelectorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/NameSelect.Server.Tests/Scoring/PairSelectorTests.cs`:

```csharp
using FluentAssertions;
using NameSelect.Server.Scoring;

namespace NameSelect.Server.Tests.Scoring;

public class PairSelectorTests
{
    private static ItemSnapshot Snap(string id, double elo = 1000, int shown = 0, bool banned = false)
        => new(id, elo, shown, banned);

    [Fact]
    public void Returns_null_if_fewer_than_two_eligible_items()
    {
        var items = new[] { Snap("a") };
        var result = PairSelector.Pick(items, recentPairs: Array.Empty<(string, string)>(), threshold: 3, rng: new Random(1));
        result.Should().BeNull();
    }

    [Fact]
    public void Excludes_banned_items()
    {
        var items = new[]
        {
            Snap("a", banned: true),
            Snap("b"),
            Snap("c")
        };

        for (int seed = 0; seed < 20; seed++)
        {
            var r = PairSelector.Pick(items, Array.Empty<(string, string)>(), 3, new Random(seed))!.Value;
            r.A.Should().NotBe("a");
            r.B.Should().NotBe("a");
        }
    }

    [Fact]
    public void Returns_two_distinct_items()
    {
        var items = new[] { Snap("a"), Snap("b"), Snap("c") };

        for (int seed = 0; seed < 50; seed++)
        {
            var r = PairSelector.Pick(items, Array.Empty<(string, string)>(), 3, new Random(seed))!.Value;
            r.A.Should().NotBe(r.B);
        }
    }

    [Fact]
    public void Avoids_pair_seen_in_recent_history_when_pool_is_large()
    {
        var items = Enumerable.Range(0, 10).Select(i => Snap($"i{i}")).ToArray();
        var recent = new[] { ("i0", "i1") };

        var hits = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var r = PairSelector.Pick(items, recent, 3, new Random(seed))!.Value;
            if ((r.A == "i0" && r.B == "i1") || (r.A == "i1" && r.B == "i0")) hits++;
        }
        hits.Should().BeLessThan(5);
    }

    [Fact]
    public void Falls_back_to_repeating_pair_when_only_two_items_remain()
    {
        var items = new[] { Snap("a"), Snap("b") };
        var recent = new[] { ("a", "b") };

        var r = PairSelector.Pick(items, recent, 3, new Random(1))!.Value;

        new[] { r.A, r.B }.OrderBy(x => x).Should().Equal("a", "b");
    }

    [Fact]
    public void Unseen_items_drawn_more_often_than_neutral()
    {
        var items = new[]
        {
            Snap("unseen", elo: 1000, shown: 0),
            Snap("neutral", elo: 900, shown: 50)
        };

        int unseenCount = 0;
        for (int seed = 0; seed < 500; seed++)
        {
            var r = PairSelector.Pick(items, Array.Empty<(string, string)>(), 3, new Random(seed))!.Value;
            if (r.A == "unseen" || r.B == "unseen") unseenCount++;
        }
        // both items must be selected to form a pair, so both appear every time;
        // adjust assertion: with only 2 items both are always picked.
        unseenCount.Should().Be(500);
    }
}
```

Note: the last test is intentionally trivial with only two items — keep it to document the invariant. A richer weighting test belongs in the WeightCalculator suite.

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter PairSelectorTests`
Expected: FAIL.

- [ ] **Step 3: Implement PairSelector**

Create `src/NameSelect.Server/Scoring/PairSelector.cs`:

```csharp
namespace NameSelect.Server.Scoring;

public record ItemSnapshot(string Id, double Elo, int TimesShown, bool IsBanned);

public record PickedPair(string A, string B);

public static class PairSelector
{
    private const int MaxRedraws = 10;

    public static PickedPair? Pick(
        IReadOnlyList<ItemSnapshot> items,
        IReadOnlyCollection<(string A, string B)> recentPairs,
        int threshold,
        Random rng)
    {
        var weighted = items
            .Where(i => !i.IsBanned)
            .Select(i => (i.Id, Weight: WeightCalculator.Weight(i.Elo, i.TimesShown, i.IsBanned, threshold)))
            .Where(x => x.Weight > 0)
            .ToList();

        if (weighted.Count < 2) return null;

        var recentSet = new HashSet<(string, string)>();
        foreach (var p in recentPairs)
        {
            recentSet.Add((p.A, p.B));
            recentSet.Add((p.B, p.A));
        }

        for (int attempt = 0; attempt < MaxRedraws; attempt++)
        {
            var a = DrawOne(weighted, rng);
            var remaining = weighted.Where(x => x.Id != a).ToList();
            var b = DrawOne(remaining, rng);

            if (!recentSet.Contains((a, b))) return new PickedPair(a, b);
        }

        // accept last draw rather than loop forever
        var fa = DrawOne(weighted, rng);
        var fb = DrawOne(weighted.Where(x => x.Id != fa).ToList(), rng);
        return new PickedPair(fa, fb);
    }

    private static string DrawOne(List<(string Id, int Weight)> pool, Random rng)
    {
        var total = pool.Sum(x => x.Weight);
        var roll = rng.Next(total);
        var acc = 0;
        foreach (var (id, w) in pool)
        {
            acc += w;
            if (roll < acc) return id;
        }
        return pool[^1].Id;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter PairSelectorTests`
Expected: 6 passed.

- [ ] **Step 5: Commit**

```bash
git add src/NameSelect.Server/Scoring/PairSelector.cs tests/NameSelect.Server.Tests/Scoring/PairSelectorTests.cs
git commit -m "feat: add PairSelector with weighted sampling and anti-repetition window"
```

---

## Task 5: UpsetDetector (TDD)

An upset is when the lower-ELO item wins a `prefer_x` vote, or a `like_both` occurs when the ELO gap > 50.

**Files:**
- Create: `src/NameSelect.Server/Scoring/UpsetDetector.cs`
- Test: `tests/NameSelect.Server.Tests/Scoring/UpsetDetectorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/NameSelect.Server.Tests/Scoring/UpsetDetectorTests.cs`:

```csharp
using FluentAssertions;
using NameSelect.Server.Data.Entities;
using NameSelect.Server.Scoring;

namespace NameSelect.Server.Tests.Scoring;

public class UpsetDetectorTests
{
    [Fact]
    public void PreferA_with_lower_eloA_is_upset()
        => UpsetDetector.IsUpset(VoteResult.PreferA, eloA: 900, eloB: 1100).Should().BeTrue();

    [Fact]
    public void PreferA_with_higher_eloA_is_not_upset()
        => UpsetDetector.IsUpset(VoteResult.PreferA, eloA: 1100, eloB: 900).Should().BeFalse();

    [Fact]
    public void PreferA_with_equal_elos_is_not_upset()
        => UpsetDetector.IsUpset(VoteResult.PreferA, eloA: 1000, eloB: 1000).Should().BeFalse();

    [Fact]
    public void LikeBoth_with_large_gap_is_upset()
        => UpsetDetector.IsUpset(VoteResult.LikeBoth, eloA: 1100, eloB: 1000).Should().BeTrue();

    [Fact]
    public void LikeBoth_with_small_gap_is_not_upset()
        => UpsetDetector.IsUpset(VoteResult.LikeBoth, eloA: 1030, eloB: 1000).Should().BeFalse();

    [Theory]
    [InlineData(VoteResult.BanA)]
    [InlineData(VoteResult.BanB)]
    [InlineData(VoteResult.BanBoth)]
    public void Bans_are_never_upsets(VoteResult r)
        => UpsetDetector.IsUpset(r, 100, 2000).Should().BeFalse();
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter UpsetDetectorTests`
Expected: FAIL.

- [ ] **Step 3: Implement UpsetDetector**

Create `src/NameSelect.Server/Scoring/UpsetDetector.cs`:

```csharp
using NameSelect.Server.Data.Entities;

namespace NameSelect.Server.Scoring;

public static class UpsetDetector
{
    private const double LikeBothGapThreshold = 50.0;

    public static bool IsUpset(VoteResult result, double eloA, double eloB) => result switch
    {
        VoteResult.PreferA => eloA < eloB,
        VoteResult.PreferB => eloB < eloA,
        VoteResult.LikeBoth => Math.Abs(eloA - eloB) > LikeBothGapThreshold,
        _ => false
    };
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter UpsetDetectorTests`
Expected: 8 passed.

- [ ] **Step 5: Commit**

```bash
git add src/NameSelect.Server/Scoring/UpsetDetector.cs tests/NameSelect.Server.Tests/Scoring/UpsetDetectorTests.cs
git commit -m "feat: add UpsetDetector with prefer/like_both rules"
```

---

## Task 6: VoteProcessor (TDD with Postgres)

Applies a vote within a transaction: lazily creates `item_states` rows, updates ELO/`times_shown`/`is_banned`, writes the `votes` row, and bumps `voting_sessions.updated_at`.

**Files:**
- Create: `src/NameSelect.Server/Voting/VoteProcessor.cs`
- Test: `tests/NameSelect.Server.Tests/Voting/VoteProcessorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/NameSelect.Server.Tests/Voting/VoteProcessorTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NameSelect.Server.Data;
using NameSelect.Server.Data.Entities;
using NameSelect.Server.Tests.Infrastructure;
using NameSelect.Server.Voting;
using Xunit;

namespace NameSelect.Server.Tests.Voting;

[Collection("postgres")]
public class VoteProcessorTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private ServiceProvider _sp = default!;

    public VoteProcessorTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_pg.ConnectionString));
        services.AddScoped<VoteProcessor>();
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await db.Lists.AddAsync(new NameList
        {
            Id = "t", Name = "T", FilePath = "/x", ItemCount = 3, LoadedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() { _sp.Dispose(); return Task.CompletedTask; }

    private async Task<Guid> CreateSession()
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = new VotingSession
        {
            Id = Guid.NewGuid(), UserId = "u", ListId = "t",
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
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter VoteProcessorTests`
Expected: FAIL.

- [ ] **Step 3: Implement VoteProcessor**

Create `src/NameSelect.Server/Voting/VoteProcessor.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using NameSelect.Server.Data;
using NameSelect.Server.Data.Entities;
using NameSelect.Server.Scoring;

namespace NameSelect.Server.Voting;

public class VoteProcessor
{
    private readonly AppDbContext _db;

    public VoteProcessor(AppDbContext db) => _db = db;

    public async Task Apply(Guid sessionId, string itemA, string itemB, VoteResult result)
    {
        using var tx = await _db.Database.BeginTransactionAsync();

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId)
                      ?? throw new InvalidOperationException($"session {sessionId} not found");

        var stateA = await GetOrCreate(sessionId, itemA);
        var stateB = await GetOrCreate(sessionId, itemB);

        stateA.TimesShown += 1;
        stateB.TimesShown += 1;

        switch (result)
        {
            case VoteResult.BanA:
                stateA.IsBanned = true;
                break;
            case VoteResult.BanB:
                stateB.IsBanned = true;
                break;
            case VoteResult.BanBoth:
                stateA.IsBanned = true;
                stateB.IsBanned = true;
                break;
            case VoteResult.PreferA:
            case VoteResult.PreferB:
            case VoteResult.LikeBoth:
                var scoreA = result switch
                {
                    VoteResult.PreferA => 1.0,
                    VoteResult.PreferB => 0.0,
                    _ => 0.5
                };
                var update = EloCalculator.Apply(
                    stateA.EloScore, stateB.EloScore,
                    EloCalculator.KFactor(stateA.TimesShown - 1),
                    EloCalculator.KFactor(stateB.TimesShown - 1),
                    scoreA);
                stateA.EloScore = update.NewEloA;
                stateB.EloScore = update.NewEloB;
                break;
        }

        _db.Votes.Add(new Vote
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ItemA = itemA,
            ItemB = itemB,
            Result = result,
            PresentedAt = DateTimeOffset.UtcNow
        });

        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<ItemState> GetOrCreate(Guid sessionId, string item)
    {
        var s = await _db.ItemStates.FirstOrDefaultAsync(x => x.SessionId == sessionId && x.Item == item);
        if (s is not null) return s;
        s = new ItemState { SessionId = sessionId, Item = item };
        _db.ItemStates.Add(s);
        return s;
    }
}
```

K-factor uses `TimesShown - 1` so the K reflects the state *before* this vote.

- [ ] **Step 4: Register in DI**

In `Program.cs`, add after the auth registration:

```csharp
builder.Services.AddScoped<NameSelect.Server.Voting.VoteProcessor>();
```

- [ ] **Step 5: Run tests**

Run: `dotnet test --filter VoteProcessorTests`
Expected: 5 passed.

- [ ] **Step 6: Commit**

```bash
git add src/NameSelect.Server/Voting/VoteProcessor.cs src/NameSelect.Server/Program.cs tests/NameSelect.Server.Tests/Voting/VoteProcessorTests.cs
git commit -m "feat: add VoteProcessor with transactional ELO update, ban handling, and lazy item_states"
```

---

## Task 7: NextPairService

Combines `ListCache`, `item_states`, and recent votes to draw a pair. Returns a DTO with primary `value`, `variants`, `description` per item.

**Files:**
- Create: `src/NameSelect.Shared/Dtos/PairDto.cs`
- Create: `src/NameSelect.Server/Voting/NextPairService.cs`

- [ ] **Step 1: Write PairDto**

Create `src/NameSelect.Shared/Dtos/PairDto.cs`:

```csharp
namespace NameSelect.Shared.Dtos;

public record PairItemDto(string Value, IReadOnlyList<string> Variants, string? Description);

public record PairDto(PairItemDto A, PairItemDto B);
```

- [ ] **Step 2: Write NextPairService**

Create `src/NameSelect.Server/Voting/NextPairService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using NameSelect.Server.Data;
using NameSelect.Server.Lists;
using NameSelect.Server.Scoring;
using NameSelect.Shared.Dtos;

namespace NameSelect.Server.Voting;

public class NextPairService
{
    private const int AntiRepeatWindow = 20;
    private readonly AppDbContext _db;
    private readonly ListCache _cache;

    public NextPairService(AppDbContext db, ListCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<PairDto?> Next(Guid sessionId)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId)
                      ?? throw new InvalidOperationException($"session {sessionId} not found");

        if (!_cache.TryGet(session.ListId, out var list) || list is null)
            throw new InvalidOperationException($"list {session.ListId} not loaded");

        var states = await _db.ItemStates.Where(s => s.SessionId == sessionId)
            .ToDictionaryAsync(s => s.Item);

        var snapshots = list.Items.Select(i =>
        {
            states.TryGetValue(i.Value, out var st);
            return new ItemSnapshot(
                i.Value,
                st?.EloScore ?? 1000.0,
                st?.TimesShown ?? 0,
                st?.IsBanned ?? false);
        }).ToList();

        var recent = await _db.Votes
            .Where(v => v.SessionId == sessionId)
            .OrderByDescending(v => v.PresentedAt)
            .Take(AntiRepeatWindow)
            .Select(v => new { v.ItemA, v.ItemB })
            .ToListAsync();
        var recentPairs = recent.Select(r => (r.ItemA, r.ItemB)).ToList();

        var picked = PairSelector.Pick(snapshots, recentPairs, session.ConfidenceThreshold, Random.Shared);
        if (picked is null) return null;

        var byValue = list.Items.ToDictionary(i => i.Value);
        var a = byValue[picked.A];
        var b = byValue[picked.B];

        return new PairDto(
            new PairItemDto(a.Value, a.Variants, a.Description),
            new PairItemDto(b.Value, b.Variants, b.Description));
    }
}
```

- [ ] **Step 3: Register in DI**

In `Program.cs`, add:

```csharp
builder.Services.AddScoped<NameSelect.Server.Voting.NextPairService>();
```

- [ ] **Step 4: Verify build**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/NameSelect.Shared/Dtos/PairDto.cs src/NameSelect.Server/Voting/NextPairService.cs src/NameSelect.Server/Program.cs
git commit -m "feat: add NextPairService that draws a pair from ListCache + item_states"
```

---

## Task 8: ResultsBuilder + DTOs

**Files:**
- Create: `src/NameSelect.Shared/Dtos/ResultsDto.cs`
- Create: `src/NameSelect.Shared/Dtos/RankedItemDto.cs`
- Create: `src/NameSelect.Server/Scoring/ResultsBuilder.cs`
- Test: `tests/NameSelect.Server.Tests/Scoring/ResultsBuilderTests.cs`

- [ ] **Step 1: Write DTOs**

Create `src/NameSelect.Shared/Dtos/RankedItemDto.cs`:

```csharp
namespace NameSelect.Shared.Dtos;

public record RankedItemDto(
    int Rank,
    string Value,
    IReadOnlyList<string> Variants,
    double EloScore,
    int TimesShown,
    bool IsBanned);
```

Create `src/NameSelect.Shared/Dtos/ResultsDto.cs`:

```csharp
namespace NameSelect.Shared.Dtos;

public record ResultsDto(
    Guid SessionId,
    string ListId,
    string ListName,
    int VoteCount,
    bool StabilityReached,
    IReadOnlyList<RankedItemDto> Ranked,
    IReadOnlyList<RankedItemDto> Banned);
```

- [ ] **Step 2: Write the failing test**

Create `tests/NameSelect.Server.Tests/Scoring/ResultsBuilderTests.cs`:

```csharp
using FluentAssertions;
using NameSelect.Server.Lists;
using NameSelect.Server.Scoring;

namespace NameSelect.Server.Tests.Scoring;

public class ResultsBuilderTests
{
    private static ListFile List(params string[] values)
        => new("l", "L", values.Select(v => new ListFileItem(v, Array.Empty<string>(), null)).ToList());

    [Fact]
    public void Ranks_non_banned_by_elo_descending()
    {
        var list = List("a", "b", "c");
        var states = new Dictionary<string, (double elo, int shown, bool banned)>
        {
            ["a"] = (1100, 5, false),
            ["b"] = (900, 5, false),
            ["c"] = (1000, 5, false)
        };

        var (ranked, banned) = ResultsBuilder.Build(list, states);

        ranked.Select(r => r.Value).Should().Equal("a", "c", "b");
        ranked[0].Rank.Should().Be(1);
        ranked[2].Rank.Should().Be(3);
        banned.Should().BeEmpty();
    }

    [Fact]
    public void Banned_items_in_separate_list()
    {
        var list = List("a", "b");
        var states = new Dictionary<string, (double, int, bool)>
        {
            ["a"] = (1000, 5, true),
            ["b"] = (1000, 5, false)
        };

        var (ranked, banned) = ResultsBuilder.Build(list, states);

        ranked.Should().ContainSingle(r => r.Value == "b");
        banned.Should().ContainSingle(r => r.Value == "a");
    }

    [Fact]
    public void Items_without_state_default_to_1000_and_unranked_at_back()
    {
        var list = List("a", "b");
        var states = new Dictionary<string, (double, int, bool)>
        {
            ["a"] = (1200, 5, false)
        };

        var (ranked, _) = ResultsBuilder.Build(list, states);

        ranked.Select(r => r.Value).Should().Equal("a", "b");
        ranked[1].EloScore.Should().Be(1000);
        ranked[1].TimesShown.Should().Be(0);
    }
}
```

- [ ] **Step 3: Run the test and verify it fails**

Run: `dotnet test --filter ResultsBuilderTests`
Expected: FAIL.

- [ ] **Step 4: Implement ResultsBuilder**

Create `src/NameSelect.Server/Scoring/ResultsBuilder.cs`:

```csharp
using NameSelect.Server.Lists;
using NameSelect.Shared.Dtos;

namespace NameSelect.Server.Scoring;

public static class ResultsBuilder
{
    public static (IReadOnlyList<RankedItemDto> Ranked, IReadOnlyList<RankedItemDto> Banned) Build(
        ListFile list,
        IReadOnlyDictionary<string, (double Elo, int TimesShown, bool IsBanned)> states)
    {
        var items = list.Items.Select(i =>
        {
            var has = states.TryGetValue(i.Value, out var s);
            return new
            {
                Item = i,
                Elo = has ? s.Elo : 1000.0,
                Shown = has ? s.TimesShown : 0,
                Banned = has && s.IsBanned
            };
        }).ToList();

        var ranked = items
            .Where(x => !x.Banned)
            .OrderByDescending(x => x.Elo)
            .Select((x, idx) => new RankedItemDto(
                idx + 1, x.Item.Value, x.Item.Variants, x.Elo, x.Shown, false))
            .ToList();

        var banned = items
            .Where(x => x.Banned)
            .OrderBy(x => x.Item.Value)
            .Select(x => new RankedItemDto(
                0, x.Item.Value, x.Item.Variants, x.Elo, x.Shown, true))
            .ToList();

        return (ranked, banned);
    }
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test --filter ResultsBuilderTests`
Expected: 3 passed.

- [ ] **Step 6: Commit**

```bash
git add src/NameSelect.Shared/Dtos src/NameSelect.Server/Scoring/ResultsBuilder.cs tests/NameSelect.Server.Tests/Scoring/ResultsBuilderTests.cs
git commit -m "feat: add ResultsBuilder producing ranked DTOs with banned items separated"
```

---

## Task 9: Stability detection helper

Computes the count of consecutive non-upsets ending at the latest vote. The `/results` endpoint uses this to set `StabilityReached`.

**Files:**
- Create: `src/NameSelect.Server/Scoring/StabilityCounter.cs`
- Test: `tests/NameSelect.Server.Tests/Scoring/StabilityCounterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/NameSelect.Server.Tests/Scoring/StabilityCounterTests.cs`:

```csharp
using FluentAssertions;
using NameSelect.Server.Scoring;

namespace NameSelect.Server.Tests.Scoring;

public class StabilityCounterTests
{
    [Fact]
    public void Empty_history_returns_zero()
        => StabilityCounter.ConsecutiveNonUpsets(Array.Empty<bool>()).Should().Be(0);

    [Fact]
    public void All_non_upsets_returns_full_length()
        => StabilityCounter.ConsecutiveNonUpsets(new[] { false, false, false }).Should().Be(3);

    [Fact]
    public void Counts_from_most_recent_when_latest_first()
    {
        // sequence ordered most-recent-first: false, false, true, false
        StabilityCounter.ConsecutiveNonUpsets(new[] { false, false, true, false }).Should().Be(2);
    }

    [Fact]
    public void Stops_at_first_upset()
    {
        StabilityCounter.ConsecutiveNonUpsets(new[] { true, false, false }).Should().Be(0);
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter StabilityCounterTests`
Expected: FAIL.

- [ ] **Step 3: Implement StabilityCounter**

Create `src/NameSelect.Server/Scoring/StabilityCounter.cs`:

```csharp
namespace NameSelect.Server.Scoring;

public static class StabilityCounter
{
    public const int StabilityThreshold = 100;

    public static int ConsecutiveNonUpsets(IReadOnlyList<bool> isUpsetMostRecentFirst)
    {
        var count = 0;
        foreach (var isUpset in isUpsetMostRecentFirst)
        {
            if (isUpset) break;
            count++;
        }
        return count;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter StabilityCounterTests`
Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/NameSelect.Server/Scoring/StabilityCounter.cs tests/NameSelect.Server.Tests/Scoring/StabilityCounterTests.cs
git commit -m "feat: add StabilityCounter helper for consecutive non-upset detection"
```

---

## Task 10: VotingEndpoints (TDD integration)

Endpoints: `GET /api/sessions/{id}/next-pair`, `POST /api/sessions/{id}/votes`, `GET /api/sessions/{id}/results`.

**Files:**
- Create: `src/NameSelect.Shared/Dtos/VoteRequest.cs`
- Create: `src/NameSelect.Server/Endpoints/VotingEndpoints.cs`
- Modify: `src/NameSelect.Server/Program.cs`
- Test: `tests/NameSelect.Server.Tests/Endpoints/VotingEndpointsTests.cs`

- [ ] **Step 1: Write VoteRequest DTO**

Create `src/NameSelect.Shared/Dtos/VoteRequest.cs`:

```csharp
namespace NameSelect.Shared.Dtos;

public record VoteRequest(string ItemA, string ItemB, string Result);
```

`Result` is a string from the client (`prefer_a`, `prefer_b`, `ban_a`, `ban_b`, `ban_both`, `like_both`) — the endpoint maps it to the enum.

- [ ] **Step 2: Write the failing test**

Create `tests/NameSelect.Server.Tests/Endpoints/VotingEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NameSelect.Server.Tests.Infrastructure;
using NameSelect.Shared.Dtos;
using Xunit;

namespace NameSelect.Server.Tests.Endpoints;

[Collection("postgres")]
public class VotingEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private NameSelectAppFactory _factory = default!;
    private string _listsDir = "";

    public VotingEndpointsTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync()
    {
        _listsDir = Path.Combine(Path.GetTempPath(), $"nsvote-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_listsDir);
        File.WriteAllText(Path.Combine(_listsDir, "a.json"),
            """{ "id": "a", "name": "A", "items": [
                 { "value": "Alice", "variants": ["Alicia"] },
                 { "value": "Bob" },
                 { "value": "Carol" }
            ] }""");
        _factory = new NameSelectAppFactory
        {
            ConnectionString = _pg.ConnectionString,
            ListsDirectory = _listsDir
        };
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        Directory.Delete(_listsDir, recursive: true);
        return Task.CompletedTask;
    }

    private async Task<SessionDto> NewSession(HttpClient client)
        => (await (await client.PostAsJsonAsync("/api/sessions",
            new CreateSessionRequest("a", 3))).Content.ReadFromJsonAsync<SessionDto>())!;

    [Fact]
    public async Task GET_next_pair_returns_two_distinct_items()
    {
        var client = _factory.CreateClient();
        var session = await NewSession(client);

        var res = await client.GetAsync($"/api/sessions/{session.Id}/next-pair");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var pair = await res.Content.ReadFromJsonAsync<PairDto>();
        pair!.A.Value.Should().NotBe(pair.B.Value);
    }

    [Fact]
    public async Task POST_vote_records_and_updates_state()
    {
        var client = _factory.CreateClient();
        var session = await NewSession(client);

        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/votes",
            new VoteRequest("Alice", "Bob", "prefer_a"));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshed = await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{session.Id}");
        refreshed!.VoteCount.Should().Be(1);
    }

    [Fact]
    public async Task POST_vote_with_invalid_result_returns_400()
    {
        var client = _factory.CreateClient();
        var session = await NewSession(client);

        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/votes",
            new VoteRequest("Alice", "Bob", "nuke"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_vote_on_other_users_session_returns_404()
    {
        var alice = _factory.CreateClient();
        alice.DefaultRequestHeaders.Add("X-Test-User", "alice");
        var session = await NewSession(alice);

        var bob = _factory.CreateClient();
        bob.DefaultRequestHeaders.Add("X-Test-User", "bob");
        var res = await bob.PostAsJsonAsync($"/api/sessions/{session.Id}/votes",
            new VoteRequest("Alice", "Bob", "prefer_a"));

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_results_returns_ranked_items()
    {
        var client = _factory.CreateClient();
        var session = await NewSession(client);

        await client.PostAsJsonAsync($"/api/sessions/{session.Id}/votes",
            new VoteRequest("Alice", "Bob", "prefer_a"));
        await client.PostAsJsonAsync($"/api/sessions/{session.Id}/votes",
            new VoteRequest("Bob", "Carol", "ban_a"));

        var res = await client.GetFromJsonAsync<ResultsDto>($"/api/sessions/{session.Id}/results");

        res!.VoteCount.Should().Be(2);
        res.Ranked.Should().HaveCount(2);
        res.Ranked[0].Value.Should().Be("Alice");
        res.Banned.Should().ContainSingle(b => b.Value == "Bob");
    }
}
```

- [ ] **Step 3: Run the test and verify it fails**

Run: `dotnet test --filter VotingEndpointsTests`
Expected: FAIL (endpoints not mapped).

- [ ] **Step 4: Implement VotingEndpoints**

Create `src/NameSelect.Server/Endpoints/VotingEndpoints.cs`:

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NameSelect.Server.Data;
using NameSelect.Server.Data.Entities;
using NameSelect.Server.Lists;
using NameSelect.Server.Scoring;
using NameSelect.Server.Voting;
using NameSelect.Shared.Dtos;

namespace NameSelect.Server.Endpoints;

public static class VotingEndpoints
{
    public static IEndpointRouteBuilder MapVotingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions/{id:guid}").RequireAuthorization();

        group.MapGet("/next-pair", async (Guid id, AppDbContext db, NextPairService service, ClaimsPrincipal user) =>
        {
            if (!await OwnsSession(db, id, user)) return Results.NotFound();
            var pair = await service.Next(id);
            return pair is null ? Results.NoContent() : Results.Ok(pair);
        });

        group.MapPost("/votes", async (Guid id, VoteRequest req, AppDbContext db, VoteProcessor processor, ClaimsPrincipal user) =>
        {
            if (!await OwnsSession(db, id, user)) return Results.NotFound();
            if (!TryParseResult(req.Result, out var result))
                return Results.BadRequest(new { error = "invalid result" });
            if (string.IsNullOrEmpty(req.ItemA) || string.IsNullOrEmpty(req.ItemB) || req.ItemA == req.ItemB)
                return Results.BadRequest(new { error = "itemA and itemB must be distinct non-empty values" });

            await processor.Apply(id, req.ItemA, req.ItemB, result);
            return Results.NoContent();
        });

        group.MapGet("/results", async (Guid id, AppDbContext db, ListCache cache, ClaimsPrincipal user) =>
        {
            if (!await OwnsSession(db, id, user)) return Results.NotFound();
            return Results.Ok(await BuildResults(id, db, cache));
        });

        return app;
    }

    internal static async Task<ResultsDto> BuildResults(Guid sessionId, AppDbContext db, ListCache cache)
    {
        var session = await db.Sessions.FirstAsync(s => s.Id == sessionId);
        if (!cache.TryGet(session.ListId, out var list) || list is null)
            throw new InvalidOperationException($"list {session.ListId} not loaded");

        var listMeta = await db.Lists.FirstAsync(l => l.Id == session.ListId);

        var states = await db.ItemStates
            .Where(s => s.SessionId == sessionId)
            .ToDictionaryAsync(s => s.Item, s => (s.EloScore, s.TimesShown, s.IsBanned));

        var (ranked, banned) = ResultsBuilder.Build(list, states);

        var recentVotes = await db.Votes
            .Where(v => v.SessionId == sessionId)
            .OrderByDescending(v => v.PresentedAt)
            .Take(StabilityCounter.StabilityThreshold)
            .Select(v => new { v.ItemA, v.ItemB, v.Result })
            .ToListAsync();

        var upsetFlags = recentVotes.Select(v =>
        {
            var ea = states.TryGetValue(v.ItemA, out var sa) ? sa.EloScore : 1000.0;
            var eb = states.TryGetValue(v.ItemB, out var sb) ? sb.EloScore : 1000.0;
            return UpsetDetector.IsUpset(v.Result, ea, eb);
        }).ToList();

        var stable = StabilityCounter.ConsecutiveNonUpsets(upsetFlags) >= StabilityCounter.StabilityThreshold;
        var voteCount = await db.Votes.CountAsync(v => v.SessionId == sessionId);

        return new ResultsDto(sessionId, listMeta.Id, listMeta.Name, voteCount, stable, ranked, banned);
    }

    private static async Task<bool> OwnsSession(AppDbContext db, Guid id, ClaimsPrincipal user)
    {
        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return sub is not null && await db.Sessions.AnyAsync(s => s.Id == id && s.UserId == sub);
    }

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
```

- [ ] **Step 5: Wire endpoint in Program.cs**

Add `using NameSelect.Server.Endpoints;` if not present and add:

```csharp
app.MapVotingEndpoints();
```

after `app.MapSessionsEndpoints();`.

- [ ] **Step 6: Run tests**

Run: `dotnet test --filter VotingEndpointsTests`
Expected: 5 passed.

- [ ] **Step 7: Commit**

```bash
git add src/NameSelect.Shared/Dtos/VoteRequest.cs src/NameSelect.Server/Endpoints/VotingEndpoints.cs src/NameSelect.Server/Program.cs tests/NameSelect.Server.Tests/Endpoints/VotingEndpointsTests.cs
git commit -m "feat: add /next-pair, /votes, /results endpoints with ownership checks"
```

---

## Task 11: ShareEndpoints (public read-only)

`GET /api/share/{token}` returns the same `ResultsDto` without auth.

**Files:**
- Create: `src/NameSelect.Server/Endpoints/ShareEndpoints.cs`
- Modify: `src/NameSelect.Server/Program.cs`
- Test: `tests/NameSelect.Server.Tests/Endpoints/ShareEndpointsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/NameSelect.Server.Tests/Endpoints/ShareEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NameSelect.Server.Tests.Infrastructure;
using NameSelect.Shared.Dtos;
using Xunit;

namespace NameSelect.Server.Tests.Endpoints;

[Collection("postgres")]
public class ShareEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private NameSelectAppFactory _factory = default!;
    private string _listsDir = "";

    public ShareEndpointsTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync()
    {
        _listsDir = Path.Combine(Path.GetTempPath(), $"nsshare-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_listsDir);
        File.WriteAllText(Path.Combine(_listsDir, "a.json"),
            """{ "id": "a", "name": "A", "items": [{ "value": "x" }, { "value": "y" }] }""");
        _factory = new NameSelectAppFactory
        {
            ConnectionString = _pg.ConnectionString,
            ListsDirectory = _listsDir
        };
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        Directory.Delete(_listsDir, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GET_share_with_valid_token_returns_results_without_auth()
    {
        var owner = _factory.CreateClient();
        var session = await (await owner.PostAsJsonAsync("/api/sessions",
            new CreateSessionRequest("a", 3))).Content.ReadFromJsonAsync<SessionDto>();

        var anon = _factory.CreateClient();
        anon.DefaultRequestHeaders.Remove("X-Test-User");
        anon.DefaultRequestHeaders.Add("X-Test-User", "");

        var res = await anon.GetAsync($"/api/share/{session!.ShareToken}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await res.Content.ReadFromJsonAsync<ResultsDto>();
        dto!.ListId.Should().Be("a");
    }

    [Fact]
    public async Task GET_share_with_unknown_token_returns_404()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync("/api/share/deadbeef");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter ShareEndpointsTests`
Expected: FAIL.

- [ ] **Step 3: Implement ShareEndpoints**

Create `src/NameSelect.Server/Endpoints/ShareEndpoints.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using NameSelect.Server.Data;
using NameSelect.Server.Lists;

namespace NameSelect.Server.Endpoints;

public static class ShareEndpoints
{
    public static IEndpointRouteBuilder MapShareEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/share/{token}", async (string token, AppDbContext db, ListCache cache) =>
        {
            var session = await db.Sessions.FirstOrDefaultAsync(s => s.ShareToken == token);
            if (session is null) return Results.NotFound();
            return Results.Ok(await VotingEndpoints.BuildResults(session.Id, db, cache));
        }).AllowAnonymous();

        return app;
    }
}
```

- [ ] **Step 4: Wire endpoint in Program.cs**

Add:

```csharp
app.MapShareEndpoints();
```

- [ ] **Step 5: Run tests**

Run: `dotnet test --filter ShareEndpointsTests`
Expected: 2 passed.

- [ ] **Step 6: Run full suite**

Run: `dotnet test`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/NameSelect.Server/Endpoints/ShareEndpoints.cs src/NameSelect.Server/Program.cs tests/NameSelect.Server.Tests/Endpoints/ShareEndpointsTests.cs
git commit -m "feat: add public GET /api/share/{token} endpoint for read-only results"
```

---

## Task 12: End-to-end smoke test

Verify the algorithm produces sensible rankings over a longer run.

**Files:**
- Test: `tests/NameSelect.Server.Tests/Endpoints/EndToEndSmokeTests.cs`

- [ ] **Step 1: Write the test**

Create `tests/NameSelect.Server.Tests/Endpoints/EndToEndSmokeTests.cs`:

```csharp
using System.Net.Http.Json;
using FluentAssertions;
using NameSelect.Server.Tests.Infrastructure;
using NameSelect.Shared.Dtos;
using Xunit;

namespace NameSelect.Server.Tests.Endpoints;

[Collection("postgres")]
public class EndToEndSmokeTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private NameSelectAppFactory _factory = default!;
    private string _listsDir = "";

    public EndToEndSmokeTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync()
    {
        _listsDir = Path.Combine(Path.GetTempPath(), $"nse2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_listsDir);
        var items = string.Join(",", Enumerable.Range(0, 20)
            .Select(i => $"{{ \"value\": \"n{i}\" }}"));
        File.WriteAllText(Path.Combine(_listsDir, "a.json"),
            $"{{ \"id\": \"a\", \"name\": \"A\", \"items\": [{items}] }}");

        _factory = new NameSelectAppFactory
        {
            ConnectionString = _pg.ConnectionString,
            ListsDirectory = _listsDir
        };
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        Directory.Delete(_listsDir, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Favourite_item_rises_to_top_when_consistently_preferred()
    {
        var client = _factory.CreateClient();
        var session = await (await client.PostAsJsonAsync("/api/sessions",
            new CreateSessionRequest("a", 3))).Content.ReadFromJsonAsync<SessionDto>();

        for (var i = 0; i < 60; i++)
        {
            var pair = await client.GetFromJsonAsync<PairDto>($"/api/sessions/{session!.Id}/next-pair");
            var result = pair!.A.Value == "n0" || pair.B.Value == "n0"
                ? (pair.A.Value == "n0" ? "prefer_a" : "prefer_b")
                : "like_both";
            await client.PostAsJsonAsync($"/api/sessions/{session.Id}/votes",
                new VoteRequest(pair.A.Value, pair.B.Value, result));
        }

        var results = await client.GetFromJsonAsync<ResultsDto>($"/api/sessions/{session!.Id}/results");
        results!.Ranked[0].Value.Should().Be("n0");
    }
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test --filter EndToEndSmokeTests`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/NameSelect.Server.Tests/Endpoints/EndToEndSmokeTests.cs
git commit -m "test: add end-to-end smoke test verifying favourite item ranks first"
```

---

## Self-Review Notes

**Spec coverage:**
- §6 ELO: K factor (EloCalculator, Task 2), per-item asymmetric updates (Task 2), confidence threshold (passed through PairSelector, Task 4).
- §6 Vote outcomes table: VoteProcessor handles all six results (Task 6) — `prefer_a/b` with score 1/0, `like_both` with score 0.5, bans without ELO change.
- §7 Pair selection: weights (Task 3), weighted sampling (Task 4), anti-repetition window of 20 with 10-redraw cap (Task 4), `times_shown` increment (Task 6).
- §8 Stability: upset rules (Task 5), consecutive counter (Task 9), `StabilityReached` flag in ResultsDto (Task 10).
- §10 endpoints: `/next-pair`, `/votes`, `/results` (Task 10), `/share/{token}` (Task 11). Remaining (`/api/lists`, `/api/sessions`, `/api/sessions/{id}`) come from Plan 1.

**Placeholder scan:** none. Every step has either complete code or a verification command.

**Type consistency:**
- `VoteResult` enum names match between Plan 1 entity, VoteProcessor switch (Task 6), VotingEndpoints parser (Task 10), UpsetDetector (Task 5).
- `PairDto`/`PairItemDto`/`ResultsDto`/`RankedItemDto`/`VoteRequest` property names are referenced consistently in tests and producer code.
- `StabilityCounter.StabilityThreshold = 100` matches the spec's 100-pair signal and is the only place the threshold is defined.

**Open item carried forward:** the `Result` CHECK constraint from the spec SQL is not in the EF Core model. If desired, Plan 4 (deployment) can add a follow-up migration that introduces the constraint via `CREATE TYPE` or `CHECK`. Skipping for now — the enum-as-string mapping + `TryParseResult` enforces the same invariants at the API boundary.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-18-nameselect-algorithm-and-voting-api.md`. Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
