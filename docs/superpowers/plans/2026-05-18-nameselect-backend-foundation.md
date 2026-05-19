# Nomelo Backend Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bootstrap the Nomelo solution with ASP.NET Core, PostgreSQL via EF Core, JSON list loader, OIDC authentication against Authentik, and the foundational `/api/lists` and `/api/sessions` endpoints.

**Architecture:** Single .NET 8 solution with three projects (`Server`, `Client`, `Shared`). The server hosts Minimal APIs, EF Core (Npgsql provider), cookie + OIDC auth. A hosted service scans `/data/lists/` at startup, validates each JSON file, and upserts metadata rows into the `lists` table. Sessions are scoped per OIDC `sub` claim. No ELO logic in this plan: only CRUD on lists and sessions.

**Tech Stack:** .NET 8 (ASP.NET Core Minimal APIs), Entity Framework Core 8 + Npgsql, PostgreSQL 16, `Microsoft.AspNetCore.Authentication.OpenIdConnect`, xUnit + FluentAssertions + Testcontainers.PostgreSql for integration tests, WebApplicationFactory for endpoint tests.

---

## File Structure

```
Nomelo.sln
src/
├── Nomelo.Server/
│   ├── Nomelo.Server.csproj
│   ├── Program.cs                              # Composition root
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Auth/
│   │   └── AuthExtensions.cs                   # OIDC + Cookie setup
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Entities/
│   │       ├── NameList.cs
│   │       ├── VotingSession.cs
│   │       ├── Vote.cs
│   │       └── ItemState.cs
│   ├── Lists/
│   │   ├── ListFile.cs                         # Deserialised JSON shape
│   │   ├── ListFileLoader.cs                   # Read+validate a single file
│   │   ├── ListDirectoryScanner.cs             # Scan /data/lists
│   │   └── ListRegistrarHostedService.cs       # Startup hosted service
│   ├── Endpoints/
│   │   ├── ListsEndpoints.cs                   # /api/lists
│   │   └── SessionsEndpoints.cs                # /api/sessions
│   └── Migrations/                             # EF Core generated
├── Nomelo.Shared/
│   ├── Nomelo.Shared.csproj
│   └── Dtos/
│       ├── ListDto.cs
│       ├── SessionDto.cs
│       └── CreateSessionRequest.cs
└── Nomelo.Client/                          # placeholder, filled in Plan 3
    └── Nomelo.Client.csproj                # empty Class Library stub

tests/
└── Nomelo.Server.Tests/
    ├── Nomelo.Server.Tests.csproj
    ├── Lists/
    │   ├── ListFileLoaderTests.cs              # unit tests
    │   └── ListDirectoryScannerTests.cs        # unit tests
    ├── Endpoints/
    │   ├── ListsEndpointsTests.cs              # integration
    │   └── SessionsEndpointsTests.cs           # integration
    └── Infrastructure/
        ├── PostgresFixture.cs                  # Testcontainers fixture
        └── TestAuthHandler.cs                  # bypass OIDC in tests

lists/                                          # sample JSON lists (volume)
└── sample.json
```

**Boundaries:**
- `Lists/` owns JSON parsing and directory scanning. Pure functions where possible; the hosted service is the only async/DB consumer.
- `Endpoints/` files are thin: extract user id from claims, call DbContext, map to DTOs. No business logic.
- `Data/Entities` are POCOs. Configuration via Fluent API in `AppDbContext.OnModelCreating`.
- `Shared` contains only DTOs consumed by the client in Plan 3. No server-only types leak here.

---

## Task 1: Solution & project scaffolding

**Files:**
- Create: `Nomelo.sln`
- Create: `src/Nomelo.Server/Nomelo.Server.csproj`
- Create: `src/Nomelo.Server/Program.cs`
- Create: `src/Nomelo.Server/appsettings.json`
- Create: `src/Nomelo.Server/appsettings.Development.json`
- Create: `src/Nomelo.Shared/Nomelo.Shared.csproj`
- Create: `src/Nomelo.Client/Nomelo.Client.csproj`
- Create: `tests/Nomelo.Server.Tests/Nomelo.Server.Tests.csproj`
- Create: `.gitignore`

- [ ] **Step 1: Create solution and folders**

```bash
cd "C:\Users\TristanROCHE\Documents\Projet Perso\Nomelo"
dotnet new sln -n Nomelo
mkdir src tests
dotnet new web -n Nomelo.Server -o src/Nomelo.Server --framework net8.0
dotnet new classlib -n Nomelo.Shared -o src/Nomelo.Shared --framework net8.0
dotnet new classlib -n Nomelo.Client -o src/Nomelo.Client --framework net8.0
dotnet new xunit -n Nomelo.Server.Tests -o tests/Nomelo.Server.Tests --framework net8.0
dotnet sln add src/Nomelo.Server src/Nomelo.Shared src/Nomelo.Client tests/Nomelo.Server.Tests
dotnet add src/Nomelo.Server reference src/Nomelo.Shared
dotnet add tests/Nomelo.Server.Tests reference src/Nomelo.Server
```

- [ ] **Step 2: Add server NuGet packages**

```bash
cd src/Nomelo.Server
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.10
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.10
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.10
dotnet add package Microsoft.AspNetCore.Authentication.OpenIdConnect --version 8.0.10
dotnet add package Microsoft.AspNetCore.Authentication.Cookies --version 2.3.0
cd ../..
```

- [ ] **Step 3: Add test NuGet packages**

```bash
cd tests/Nomelo.Server.Tests
dotnet add package FluentAssertions --version 6.12.1
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 8.0.10
dotnet add package Testcontainers.PostgreSql --version 3.10.0
cd ../..
```

- [ ] **Step 4: Write minimal Program.cs**

Replace `src/Nomelo.Server/Program.cs` with:

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
```

- [ ] **Step 5: Write appsettings.json**

Replace `src/Nomelo.Server/appsettings.json` with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=nomelo;Username=ns;Password=ns"
  },
  "Lists": {
    "Directory": "/data/lists"
  },
  "OIDC": {
    "Authority": "",
    "ClientId": "",
    "ClientSecret": ""
  }
}
```

And `appsettings.Development.json`:

```json
{
  "Lists": {
    "Directory": "./lists"
  }
}
```

- [ ] **Step 6: Write .gitignore**

Create `.gitignore` at repo root with:

```
bin/
obj/
*.user
.vs/
appsettings.*.local.json
.env
node_modules/
src/Nomelo.Server/wwwroot/
```

- [ ] **Step 7: Verify build**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add Nomelo.sln src tests .gitignore
git commit -m "chore: scaffold solution with server, shared, client, tests projects"
```

---

## Task 2: Entities & DbContext

**Files:**
- Create: `src/Nomelo.Server/Data/Entities/NameList.cs`
- Create: `src/Nomelo.Server/Data/Entities/VotingSession.cs`
- Create: `src/Nomelo.Server/Data/Entities/Vote.cs`
- Create: `src/Nomelo.Server/Data/Entities/ItemState.cs`
- Create: `src/Nomelo.Server/Data/AppDbContext.cs`

- [ ] **Step 1: Write NameList entity**

Create `src/Nomelo.Server/Data/Entities/NameList.cs`:

```csharp
namespace Nomelo.Server.Data.Entities;

public class NameList
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int ItemCount { get; set; }
    public DateTimeOffset LoadedAt { get; set; }
}
```

- [ ] **Step 2: Write VotingSession entity**

Create `src/Nomelo.Server/Data/Entities/VotingSession.cs`:

```csharp
namespace Nomelo.Server.Data.Entities;

public class VotingSession
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string ListId { get; set; } = "";
    public int ConfidenceThreshold { get; set; } = 3;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? ShareToken { get; set; }
}
```

- [ ] **Step 3: Write Vote entity**

Create `src/Nomelo.Server/Data/Entities/Vote.cs`:

```csharp
namespace Nomelo.Server.Data.Entities;

public enum VoteResult
{
    PreferA,
    PreferB,
    BanA,
    BanB,
    BanBoth,
    LikeBoth
}

public class Vote
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string ItemA { get; set; } = "";
    public string ItemB { get; set; } = "";
    public VoteResult Result { get; set; }
    public DateTimeOffset PresentedAt { get; set; }
}
```

- [ ] **Step 4: Write ItemState entity**

Create `src/Nomelo.Server/Data/Entities/ItemState.cs`:

```csharp
namespace Nomelo.Server.Data.Entities;

public class ItemState
{
    public Guid SessionId { get; set; }
    public string Item { get; set; } = "";
    public double EloScore { get; set; } = 1000.0;
    public int TimesShown { get; set; }
    public bool IsBanned { get; set; }
}
```

- [ ] **Step 5: Write AppDbContext**

Create `src/Nomelo.Server/Data/AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Data.Entities;

namespace Nomelo.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<NameList> Lists => Set<NameList>();
    public DbSet<VotingSession> Sessions => Set<VotingSession>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<ItemState> ItemStates => Set<ItemState>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<NameList>(e =>
        {
            e.ToTable("lists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.FilePath).HasColumnName("file_path").IsRequired();
            e.Property(x => x.ItemCount).HasColumnName("item_count");
            e.Property(x => x.LoadedAt).HasColumnName("loaded_at");
        });

        b.Entity<VotingSession>(e =>
        {
            e.ToTable("voting_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.ListId).HasColumnName("list_id").IsRequired();
            e.Property(x => x.ConfidenceThreshold).HasColumnName("confidence_threshold").HasDefaultValue(3);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            e.Property(x => x.ShareToken).HasColumnName("share_token");
            e.HasIndex(x => x.ShareToken).IsUnique();
            e.HasIndex(x => new { x.UserId, x.ListId });
            e.HasOne<NameList>().WithMany().HasForeignKey(x => x.ListId);
        });

        b.Entity<Vote>(e =>
        {
            e.ToTable("votes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.ItemA).HasColumnName("item_a").IsRequired();
            e.Property(x => x.ItemB).HasColumnName("item_b").IsRequired();
            e.Property(x => x.Result).HasColumnName("result").HasConversion<string>();
            e.Property(x => x.PresentedAt).HasColumnName("presented_at").HasDefaultValueSql("now()");
            e.HasOne<VotingSession>().WithMany().HasForeignKey(x => x.SessionId);
            e.HasIndex(x => x.SessionId);
        });

        b.Entity<ItemState>(e =>
        {
            e.ToTable("item_states");
            e.HasKey(x => new { x.SessionId, x.Item });
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.Item).HasColumnName("item");
            e.Property(x => x.EloScore).HasColumnName("elo_score").HasDefaultValue(1000.0);
            e.Property(x => x.TimesShown).HasColumnName("times_shown").HasDefaultValue(0);
            e.Property(x => x.IsBanned).HasColumnName("is_banned").HasDefaultValue(false);
            e.HasOne<VotingSession>().WithMany().HasForeignKey(x => x.SessionId);
        });
    }
}
```

`Result` is mapped as a string for readability. The check constraint in the spec is enforced at the enum level in C#.

- [ ] **Step 6: Register DbContext in Program.cs**

Replace `src/Nomelo.Server/Program.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
```

- [ ] **Step 7: Verify build**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add src/Nomelo.Server/Data
git add src/Nomelo.Server/Program.cs
git commit -m "feat: add EF Core entities and AppDbContext for lists, sessions, votes, item states"
```

---

## Task 3: Initial EF Core migration

**Files:**
- Create: `src/Nomelo.Server/Migrations/*` (generated)

- [ ] **Step 1: Install dotnet-ef if missing**

Run: `dotnet tool install --global dotnet-ef --version 8.0.10`
Expected: Tool already installed or installed successfully.

- [ ] **Step 2: Generate initial migration**

```bash
dotnet ef migrations add InitialCreate `
  --project src/Nomelo.Server `
  --startup-project src/Nomelo.Server `
  --output-dir Migrations
```

Expected: Migration files appear under `src/Nomelo.Server/Migrations/`.

- [ ] **Step 3: Inspect generated SQL**

```bash
dotnet ef migrations script `
  --project src/Nomelo.Server `
  --startup-project src/Nomelo.Server `
  --output migration-preview.sql
```

Open `migration-preview.sql` and confirm: `lists`, `voting_sessions`, `votes`, `item_states` tables present with the columns from Task 2. Delete the preview file when done.

- [ ] **Step 4: Wire automatic migration on startup**

Replace the `var app = builder.Build();` block in `Program.cs` with:

```csharp
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
```

- [ ] **Step 5: Verify build**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Nomelo.Server/Migrations src/Nomelo.Server/Program.cs
git commit -m "feat: add initial EF Core migration and auto-migrate on startup"
```

---

## Task 4: List file loader (unit-tested)

Parses one JSON file into an in-memory `ListFile`, with validation: required fields, unique `value` within `items`.

**Files:**
- Create: `src/Nomelo.Server/Lists/ListFile.cs`
- Create: `src/Nomelo.Server/Lists/ListFileLoader.cs`
- Test: `tests/Nomelo.Server.Tests/Lists/ListFileLoaderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Nomelo.Server.Tests/Lists/ListFileLoaderTests.cs`:

```csharp
using FluentAssertions;
using Nomelo.Server.Lists;

namespace Nomelo.Server.Tests.Lists;

public class ListFileLoaderTests
{
    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nstest-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Load_valid_file_returns_parsed_list()
    {
        var path = WriteTemp("""
        {
          "id": "prenoms-fr",
          "name": "Prénoms français",
          "items": [
            { "value": "Alexandre", "variants": ["Alex"], "description": "du grec" },
            { "value": "Abigaëlle" }
          ]
        }
        """);

        var loader = new ListFileLoader();

        var result = loader.Load(path);

        result.Id.Should().Be("prenoms-fr");
        result.Name.Should().Be("Prénoms français");
        result.Items.Should().HaveCount(2);
        result.Items[0].Value.Should().Be("Alexandre");
        result.Items[0].Variants.Should().ContainSingle().Which.Should().Be("Alex");
        result.Items[1].Variants.Should().BeEmpty();
    }

    [Fact]
    public void Load_missing_id_throws()
    {
        var path = WriteTemp("""{ "name": "x", "items": [{ "value": "a" }] }""");
        var loader = new ListFileLoader();

        var act = () => loader.Load(path);

        act.Should().Throw<ListFileException>().WithMessage("*id*");
    }

    [Fact]
    public void Load_duplicate_values_throws()
    {
        var path = WriteTemp("""
        { "id": "x", "name": "x", "items": [
          { "value": "Alex" }, { "value": "Alex" }
        ] }
        """);
        var loader = new ListFileLoader();

        var act = () => loader.Load(path);

        act.Should().Throw<ListFileException>().WithMessage("*duplicate*");
    }

    [Fact]
    public void Load_empty_items_throws()
    {
        var path = WriteTemp("""{ "id": "x", "name": "x", "items": [] }""");
        var loader = new ListFileLoader();

        var act = () => loader.Load(path);

        act.Should().Throw<ListFileException>().WithMessage("*items*");
    }

    [Fact]
    public void Load_unknown_fields_are_ignored()
    {
        var path = WriteTemp("""
        { "id": "x", "name": "x", "future": "stuff",
          "items": [{ "value": "a", "weird": 1 }] }
        """);
        var loader = new ListFileLoader();

        var act = () => loader.Load(path);

        act.Should().NotThrow();
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter ListFileLoaderTests`
Expected: FAIL with compilation errors (types don't exist yet).

- [ ] **Step 3: Implement ListFile**

Create `src/Nomelo.Server/Lists/ListFile.cs`:

```csharp
namespace Nomelo.Server.Lists;

public record ListFileItem(string Value, IReadOnlyList<string> Variants, string? Description);

public record ListFile(string Id, string Name, IReadOnlyList<ListFileItem> Items);

public class ListFileException : Exception
{
    public ListFileException(string message) : base(message) { }
}
```

- [ ] **Step 4: Implement ListFileLoader**

Create `src/Nomelo.Server/Lists/ListFileLoader.cs`:

```csharp
using System.Text.Json;

namespace Nomelo.Server.Lists;

public class ListFileLoader
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ListFile Load(string path)
    {
        using var stream = File.OpenRead(path);
        var raw = JsonSerializer.Deserialize<RawList>(stream, Json)
                  ?? throw new ListFileException($"empty or invalid JSON: {path}");

        if (string.IsNullOrWhiteSpace(raw.Id))
            throw new ListFileException($"missing 'id' in {path}");
        if (string.IsNullOrWhiteSpace(raw.Name))
            throw new ListFileException($"missing 'name' in {path}");
        if (raw.Items is null || raw.Items.Count == 0)
            throw new ListFileException($"empty 'items' in {path}");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<ListFileItem>(raw.Items.Count);
        foreach (var i in raw.Items)
        {
            if (string.IsNullOrWhiteSpace(i.Value))
                throw new ListFileException($"item missing 'value' in {path}");
            if (!seen.Add(i.Value))
                throw new ListFileException($"duplicate value '{i.Value}' in {path}");
            items.Add(new ListFileItem(i.Value, i.Variants ?? Array.Empty<string>(), i.Description));
        }

        return new ListFile(raw.Id, raw.Name, items);
    }

    private sealed class RawList
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<RawItem>? Items { get; set; }
    }

    private sealed class RawItem
    {
        public string? Value { get; set; }
        public List<string>? Variants { get; set; }
        public string? Description { get; set; }
    }
}
```

- [ ] **Step 5: Run tests and verify they pass**

Run: `dotnet test --filter ListFileLoaderTests`
Expected: 5 passed.

- [ ] **Step 6: Commit**

```bash
git add src/Nomelo.Server/Lists tests/Nomelo.Server.Tests/Lists
git commit -m "feat: add ListFileLoader with JSON parsing and validation"
```

---

## Task 5: Directory scanner & startup registrar

The scanner enumerates JSON files; the hosted service runs it at startup and upserts metadata into the `lists` table. A file whose `id` already exists is updated; old `lists` rows whose file no longer exists are removed.

**Files:**
- Create: `src/Nomelo.Server/Lists/ListDirectoryScanner.cs`
- Create: `src/Nomelo.Server/Lists/ListRegistrarHostedService.cs`
- Test: `tests/Nomelo.Server.Tests/Lists/ListDirectoryScannerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Nomelo.Server.Tests/Lists/ListDirectoryScannerTests.cs`:

```csharp
using FluentAssertions;
using Nomelo.Server.Lists;

namespace Nomelo.Server.Tests.Lists;

public class ListDirectoryScannerTests : IDisposable
{
    private readonly string _dir;

    public ListDirectoryScannerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"nsdir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void WriteJson(string filename, string body) =>
        File.WriteAllText(Path.Combine(_dir, filename), body);

    [Fact]
    public void Scan_returns_all_valid_files()
    {
        WriteJson("a.json", """{ "id": "a", "name": "A", "items": [{ "value": "x" }] }""");
        WriteJson("b.json", """{ "id": "b", "name": "B", "items": [{ "value": "y" }] }""");
        var scanner = new ListDirectoryScanner(new ListFileLoader());

        var result = scanner.Scan(_dir).ToList();

        result.Should().HaveCount(2);
        result.Select(r => r.List.Id).Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void Scan_skips_non_json_files()
    {
        WriteJson("a.json", """{ "id": "a", "name": "A", "items": [{ "value": "x" }] }""");
        File.WriteAllText(Path.Combine(_dir, "notes.txt"), "ignore me");

        var scanner = new ListDirectoryScanner(new ListFileLoader());

        var result = scanner.Scan(_dir).ToList();

        result.Should().HaveCount(1);
    }

    [Fact]
    public void Scan_invalid_file_is_reported_not_thrown()
    {
        WriteJson("bad.json", """{ "name": "no id" }""");
        WriteJson("ok.json", """{ "id": "a", "name": "A", "items": [{ "value": "x" }] }""");
        var scanner = new ListDirectoryScanner(new ListFileLoader());

        var result = scanner.Scan(_dir).ToList();

        result.Should().HaveCount(2);
        result.Single(r => r.Error is not null).Error.Should().Contain("id");
        result.Single(r => r.Error is null).List.Id.Should().Be("a");
    }

    [Fact]
    public void Scan_missing_directory_returns_empty()
    {
        var scanner = new ListDirectoryScanner(new ListFileLoader());

        var result = scanner.Scan(Path.Combine(_dir, "does-not-exist")).ToList();

        result.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter ListDirectoryScannerTests`
Expected: FAIL (compilation errors).

- [ ] **Step 3: Implement ListDirectoryScanner**

Create `src/Nomelo.Server/Lists/ListDirectoryScanner.cs`:

```csharp
namespace Nomelo.Server.Lists;

public record ScanResult(string Path, ListFile? List, string? Error);

public class ListDirectoryScanner
{
    private readonly ListFileLoader _loader;

    public ListDirectoryScanner(ListFileLoader loader) => _loader = loader;

    public IEnumerable<ScanResult> Scan(string directory)
    {
        if (!Directory.Exists(directory)) yield break;

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            ListFile? list = null;
            string? error = null;
            try { list = _loader.Load(path); }
            catch (Exception ex) { error = ex.Message; }
            yield return new ScanResult(path, list, error);
        }
    }
}
```

- [ ] **Step 4: Run tests and verify they pass**

Run: `dotnet test --filter ListDirectoryScannerTests`
Expected: 4 passed.

- [ ] **Step 5: Implement ListRegistrarHostedService**

Create `src/Nomelo.Server/Lists/ListRegistrarHostedService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Data;
using Nomelo.Server.Data.Entities;

namespace Nomelo.Server.Lists;

public class ListRegistrarHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<ListRegistrarHostedService> _log;

    public ListRegistrarHostedService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<ListRegistrarHostedService> log)
    {
        _services = services;
        _config = config;
        _log = log;
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

        var toRemove = await db.Lists
            .Where(l => !seenIds.Contains(l.Id))
            .ToListAsync(ct);
        if (toRemove.Count > 0) db.Lists.RemoveRange(toRemove);

        await db.SaveChangesAsync(ct);
        _log.LogInformation("Registered {Count} lists from {Dir}", seenIds.Count, dir);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

- [ ] **Step 6: Wire scanner + hosted service in DI**

In `Program.cs`, add **after** the `AddDbContext` line:

```csharp
builder.Services.AddSingleton<ListFileLoader>();
builder.Services.AddScoped<ListDirectoryScanner>();
builder.Services.AddHostedService<ListRegistrarHostedService>();
```

Required `using` directives at the top of `Program.cs`:

```csharp
using Nomelo.Server.Lists;
```

- [ ] **Step 7: Add a sample list file**

Create `lists/sample.json`:

```json
{
  "id": "sample",
  "name": "Sample names",
  "items": [
    { "value": "Alice" },
    { "value": "Bob" },
    { "value": "Charlie" }
  ]
}
```

- [ ] **Step 8: Verify build**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 9: Commit**

```bash
git add src/Nomelo.Server/Lists tests/Nomelo.Server.Tests/Lists src/Nomelo.Server/Program.cs lists
git commit -m "feat: scan lists directory at startup and upsert metadata into lists table"
```

---

## Task 6: OIDC + Cookie authentication

OIDC against Authentik. The OIDC handler runs only when the user hits the login endpoints; API calls authenticate via the cookie set after callback. In tests, both schemes are replaced by a `TestAuthHandler` so endpoint tests don't need a real Authentik.

**Files:**
- Create: `src/Nomelo.Server/Auth/AuthExtensions.cs`
- Modify: `src/Nomelo.Server/Program.cs`

- [ ] **Step 1: Write AuthExtensions**

Create `src/Nomelo.Server/Auth/AuthExtensions.cs`:

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Nomelo.Server.Auth;

public static class AuthExtensions
{
    public const string CookieScheme = "ne-cookie";
    public const string OidcScheme = "ne-oidc";

    public static IServiceCollection AddNomeloAuth(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddAuthentication(opt =>
            {
                opt.DefaultScheme = CookieScheme;
                opt.DefaultChallengeScheme = OidcScheme;
            })
            .AddCookie(CookieScheme, opt =>
            {
                opt.Cookie.Name = "ne_auth";
                opt.Cookie.HttpOnly = true;
                opt.Cookie.SameSite = SameSiteMode.Strict;
                opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                opt.ExpireTimeSpan = TimeSpan.FromDays(30);
                opt.SlidingExpiration = true;
                opt.Events.OnRedirectToLogin = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                    ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(OidcScheme, opt =>
            {
                opt.Authority = config["OIDC:Authority"];
                opt.ClientId = config["OIDC:ClientId"];
                opt.ClientSecret = config["OIDC:ClientSecret"];
                opt.ResponseType = OpenIdConnectResponseType.Code;
                opt.UsePkce = true;
                opt.SaveTokens = false;
                opt.GetClaimsFromUserInfoEndpoint = true;
                opt.SignInScheme = CookieScheme;
                opt.Scope.Add("openid");
                opt.Scope.Add("profile");
                opt.Scope.Add("email");
                opt.CallbackPath = "/signin-oidc";
                opt.SignedOutCallbackPath = "/signout-callback-oidc";
            });

        services.AddAuthorization();
        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", (string? returnUrl) =>
            Results.Challenge(
                new() { RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl },
                new[] { OidcScheme }));

        app.MapPost("/logout", () =>
            Results.SignOut(
                new() { RedirectUri = "/" },
                new[] { CookieScheme, OidcScheme }));

        app.MapGet("/api/me", (HttpContext ctx) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();
            var sub = ctx.User.FindFirst("sub")?.Value
                      ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Results.Ok(new { userId = sub });
        });

        return app;
    }
}
```

- [ ] **Step 2: Wire auth in Program.cs**

Replace `Program.cs` contents with:

```csharp
using Microsoft.EntityFrameworkCore;
using Nomelo.Server.Auth;
using Nomelo.Server.Data;
using Nomelo.Server.Lists;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<ListFileLoader>();
builder.Services.AddScoped<ListDirectoryScanner>();
builder.Services.AddHostedService<ListRegistrarHostedService>();

builder.Services.AddNomeloAuth(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();

app.Run();

public partial class Program;
```

- [ ] **Step 3: Verify build**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Nomelo.Server/Auth src/Nomelo.Server/Program.cs
git commit -m "feat: add OIDC (Authentik) + cookie auth with /login, /logout, /api/me"
```

---

## Task 7: DTOs in Shared project

**Files:**
- Create: `src/Nomelo.Shared/Dtos/ListDto.cs`
- Create: `src/Nomelo.Shared/Dtos/SessionDto.cs`
- Create: `src/Nomelo.Shared/Dtos/CreateSessionRequest.cs`

- [ ] **Step 1: Delete default Class1.cs from Shared**

```bash
rm src/Nomelo.Shared/Class1.cs
```

- [ ] **Step 2: Write ListDto**

Create `src/Nomelo.Shared/Dtos/ListDto.cs`:

```csharp
namespace Nomelo.Shared.Dtos;

public record ListDto(string Id, string Name, int ItemCount);
```

- [ ] **Step 3: Write SessionDto**

Create `src/Nomelo.Shared/Dtos/SessionDto.cs`:

```csharp
namespace Nomelo.Shared.Dtos;

public record SessionDto(
    Guid Id,
    string ListId,
    string ListName,
    int ConfidenceThreshold,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ShareToken,
    int VoteCount);
```

- [ ] **Step 4: Write CreateSessionRequest**

Create `src/Nomelo.Shared/Dtos/CreateSessionRequest.cs`:

```csharp
namespace Nomelo.Shared.Dtos;

public record CreateSessionRequest(string ListId, int ConfidenceThreshold);
```

- [ ] **Step 5: Verify build**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Nomelo.Shared
git commit -m "feat: add ListDto, SessionDto, CreateSessionRequest in Shared"
```

---

## Task 8: Test infrastructure (Postgres fixture + fake auth)

**Files:**
- Create: `tests/Nomelo.Server.Tests/Infrastructure/PostgresFixture.cs`
- Create: `tests/Nomelo.Server.Tests/Infrastructure/TestAuthHandler.cs`
- Create: `tests/Nomelo.Server.Tests/Infrastructure/NomeloAppFactory.cs`

- [ ] **Step 1: Write PostgresFixture**

Create `tests/Nomelo.Server.Tests/Infrastructure/PostgresFixture.cs`:

```csharp
using Testcontainers.PostgreSql;
using Xunit;

namespace Nomelo.Server.Tests.Infrastructure;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("nomelo")
        .WithUsername("ns")
        .WithPassword("ns")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture> { }
```

- [ ] **Step 2: Write TestAuthHandler**

Create `tests/Nomelo.Server.Tests/Infrastructure/TestAuthHandler.cs`:

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nomelo.Server.Tests.Infrastructure;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string TestUserId = "test-user-123";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers.TryGetValue("X-Test-User", out var hdr) && !string.IsNullOrEmpty(hdr)
            ? hdr.ToString()
            : TestUserId;

        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim(ClaimTypes.NameIdentifier, userId)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

- [ ] **Step 3: Write NomeloAppFactory**

Create `tests/Nomelo.Server.Tests/Infrastructure/NomeloAppFactory.cs`:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nomelo.Server.Data;
using Nomelo.Server.Auth;

namespace Nomelo.Server.Tests.Infrastructure;

public class NomeloAppFactory : WebApplicationFactory<Program>
{
    public string ConnectionString { get; init; } = "";
    public string ListsDirectory { get; init; } = "";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Lists:Directory", ListsDirectory);
        builder.UseSetting("OIDC:Authority", "https://example.invalid/");
        builder.UseSetting("OIDC:ClientId", "test");
        builder.UseSetting("OIDC:ClientSecret", "test");

        builder.ConfigureServices(services =>
        {
            services
                .AddAuthentication(opt =>
                {
                    opt.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    opt.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }
}
```

The test factory overrides the auth registration after the app's own registration runs, so `DefaultAuthenticateScheme` points to `Test`. Endpoint tests use this directly.

- [ ] **Step 4: Verify build**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add tests/Nomelo.Server.Tests/Infrastructure
git commit -m "test: add Postgres testcontainer fixture, test auth handler, app factory"
```

---

## Task 9: /api/lists endpoint (TDD)

**Files:**
- Create: `src/Nomelo.Server/Endpoints/ListsEndpoints.cs`
- Modify: `src/Nomelo.Server/Program.cs`
- Test: `tests/Nomelo.Server.Tests/Endpoints/ListsEndpointsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Nomelo.Server.Tests/Endpoints/ListsEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nomelo.Server.Tests.Infrastructure;
using Nomelo.Shared.Dtos;
using Xunit;

namespace Nomelo.Server.Tests.Endpoints;

[Collection("postgres")]
public class ListsEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private NomeloAppFactory _factory = default!;
    private string _listsDir = "";

    public ListsEndpointsTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync()
    {
        _listsDir = Path.Combine(Path.GetTempPath(), $"nslist-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_listsDir);
        File.WriteAllText(Path.Combine(_listsDir, "a.json"),
            """{ "id": "a", "name": "List A", "items": [{ "value": "x" }, { "value": "y" }] }""");

        _factory = new NomeloAppFactory
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
    public async Task GET_api_lists_returns_registered_lists()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/lists");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var lists = await res.Content.ReadFromJsonAsync<List<ListDto>>();
        lists.Should().ContainSingle(l => l.Id == "a" && l.Name == "List A" && l.ItemCount == 2);
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter ListsEndpointsTests`
Expected: FAIL (404 — endpoint not mapped yet).

- [ ] **Step 3: Implement ListsEndpoints**

Create `src/Nomelo.Server/Endpoints/ListsEndpoints.cs`:

```csharp
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
```

- [ ] **Step 4: Wire endpoint in Program.cs**

Add `using Nomelo.Server.Endpoints;` and add this line after `app.MapAuthEndpoints();`:

```csharp
app.MapListsEndpoints();
```

- [ ] **Step 5: Run tests and verify they pass**

Run: `dotnet test --filter ListsEndpointsTests`
Expected: 1 passed.

- [ ] **Step 6: Commit**

```bash
git add src/Nomelo.Server/Endpoints src/Nomelo.Server/Program.cs tests/Nomelo.Server.Tests/Endpoints
git commit -m "feat: add GET /api/lists endpoint with integration test"
```

---

## Task 10: /api/sessions endpoints (TDD)

Three endpoints: list user's sessions, create a session (returns 201 with share token), get session details. Authorization scopes results to the current user.

**Files:**
- Create: `src/Nomelo.Server/Endpoints/SessionsEndpoints.cs`
- Modify: `src/Nomelo.Server/Program.cs`
- Test: `tests/Nomelo.Server.Tests/Endpoints/SessionsEndpointsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Nomelo.Server.Tests/Endpoints/SessionsEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nomelo.Server.Tests.Infrastructure;
using Nomelo.Shared.Dtos;
using Xunit;

namespace Nomelo.Server.Tests.Endpoints;

[Collection("postgres")]
public class SessionsEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private NomeloAppFactory _factory = default!;
    private string _listsDir = "";

    public SessionsEndpointsTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync()
    {
        _listsDir = Path.Combine(Path.GetTempPath(), $"nssess-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_listsDir);
        File.WriteAllText(Path.Combine(_listsDir, "a.json"),
            """{ "id": "a", "name": "List A", "items": [{ "value": "x" }, { "value": "y" }] }""");

        _factory = new NomeloAppFactory
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
    public async Task POST_creates_session_with_share_token()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/sessions",
            new CreateSessionRequest("a", 5));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await res.Content.ReadFromJsonAsync<SessionDto>();
        dto!.ListId.Should().Be("a");
        dto.ConfidenceThreshold.Should().Be(5);
        dto.ShareToken.Should().NotBeNullOrWhiteSpace();
        dto.VoteCount.Should().Be(0);
    }

    [Fact]
    public async Task POST_with_unknown_list_returns_404()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/sessions",
            new CreateSessionRequest("does-not-exist", 3));

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_with_invalid_threshold_returns_400()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/sessions",
            new CreateSessionRequest("a", 0));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_returns_only_current_user_sessions()
    {
        var alice = _factory.CreateClient();
        alice.DefaultRequestHeaders.Add("X-Test-User", "alice");
        await alice.PostAsJsonAsync("/api/sessions", new CreateSessionRequest("a", 3));

        var bob = _factory.CreateClient();
        bob.DefaultRequestHeaders.Add("X-Test-User", "bob");
        await bob.PostAsJsonAsync("/api/sessions", new CreateSessionRequest("a", 3));

        var aliceList = await alice.GetFromJsonAsync<List<SessionDto>>("/api/sessions");

        aliceList.Should().HaveCount(1);
    }

    [Fact]
    public async Task GET_by_id_other_user_returns_404()
    {
        var alice = _factory.CreateClient();
        alice.DefaultRequestHeaders.Add("X-Test-User", "alice");
        var created = await (await alice.PostAsJsonAsync("/api/sessions",
            new CreateSessionRequest("a", 3))).Content.ReadFromJsonAsync<SessionDto>();

        var bob = _factory.CreateClient();
        bob.DefaultRequestHeaders.Add("X-Test-User", "bob");

        var res = await bob.GetAsync($"/api/sessions/{created!.Id}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `dotnet test --filter SessionsEndpointsTests`
Expected: FAIL (404s — endpoints not mapped yet).

- [ ] **Step 3: Implement SessionsEndpoints**

Create `src/Nomelo.Server/Endpoints/SessionsEndpoints.cs`:

```csharp
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
```

- [ ] **Step 4: Wire endpoint in Program.cs**

Add this line after `app.MapListsEndpoints();`:

```csharp
app.MapSessionsEndpoints();
```

- [ ] **Step 5: Run tests and verify they pass**

Run: `dotnet test --filter SessionsEndpointsTests`
Expected: 5 passed.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Nomelo.Server/Endpoints/SessionsEndpoints.cs src/Nomelo.Server/Program.cs tests/Nomelo.Server.Tests/Endpoints/SessionsEndpointsTests.cs
git commit -m "feat: add /api/sessions endpoints (list, create, get) with share token generation"
```

---

## Task 11: Smoke test against a real local Postgres

This task verifies the app boots end-to-end without containers. It is manual; skip if running headless.

**Files:** none

- [ ] **Step 1: Start a local Postgres**

```bash
docker run --rm -d --name ns-pg -e POSTGRES_DB=nomelo -e POSTGRES_USER=ns -e POSTGRES_PASSWORD=ns -p 5432:5432 postgres:16
```

- [ ] **Step 2: Run the app**

```bash
cd src/Nomelo.Server
dotnet run --launch-profile http
```

Expected: log line "Registered 1 lists from ./lists", server listening on http://localhost:5000 (or similar).

- [ ] **Step 3: Hit /health**

```bash
curl -i http://localhost:5000/health
```

Expected: `200 OK` with `{"status":"ok"}`.

- [ ] **Step 4: Hit /api/lists unauthenticated**

```bash
curl -i http://localhost:5000/api/lists
```

Expected: `401 Unauthorized` (cookie auth rejected — confirms `RequireAuthorization()` works).

- [ ] **Step 5: Stop**

```bash
docker stop ns-pg
```

No commit — verification only.

---

## Self-Review Notes

- All entities, DTOs, scanner, loader, endpoints, and tests defined inline — no placeholders.
- Auth flow uses real OIDC in production code; tests substitute `TestAuthHandler` via `NomeloAppFactory`.
- `Program.cs` exposes `public partial class Program;` so `WebApplicationFactory<Program>` works.
- Spec coverage check: §1 problem statement (covered by intent), §2 architecture (project layout — Docker is Plan 4), §3 auth (Task 6), §4 name lists (Tasks 4–5), §5 data model (Tasks 2–3), §6–§8 ELO/pair selection/completion (out of scope, Plan 2), §9 UI (Plan 3), §10 API: `/api/lists` (Task 9), `/api/sessions` (Task 10) — remaining endpoints belong to Plan 2, §11 deployment (Plan 4).
- Type consistency: `VoteResult` enum names are PascalCase in C# and stored as strings; Plan 2 will convert to the spec's snake_case via `[EnumMember]` attributes or a value converter when introducing the votes endpoint.
- The `Result` check constraint from the SQL spec is not added in this migration. Plan 2 should add it (either via raw SQL in a migration or via a `CheckConstraint` in Fluent API) when the votes endpoint lands.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-18-nomelo-backend-foundation.md`. Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
