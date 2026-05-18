using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NameSelect.Server.Tests.Infrastructure;
using NameSelect.Shared.Dtos;
using Xunit;

namespace NameSelect.Server.Tests.Endpoints;

[Collection("postgres")]
public class SessionsEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private NameSelectAppFactory _factory = default!;
    private string _listsDir = "";

    public SessionsEndpointsTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync()
    {
        _listsDir = Path.Combine(Path.GetTempPath(), $"nssess-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_listsDir);
        File.WriteAllText(Path.Combine(_listsDir, "a.json"),
            """{ "id": "a", "name": "List A", "items": [{ "value": "x" }, { "value": "y" }] }""");

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
