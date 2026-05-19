using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nomelo.Server.Tests.Infrastructure;
using Nomelo.Shared.Dtos;
using Xunit;

namespace Nomelo.Server.Tests.Endpoints;

[Collection("postgres")]
public class VotingEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private NomeloAppFactory _factory = default!;
    private string _listsDir = "";
    private readonly string _userPrefix = $"u-{Guid.NewGuid():N}-";

    public VotingEndpointsTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync()
    {
        _listsDir = Path.Combine(Path.GetTempPath(), $"nsvote-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_listsDir);
        File.WriteAllText(Path.Combine(_listsDir, "a.json"),
            """
            { "id": "a", "name": "A", "items": [
                 { "value": "Alice", "variants": ["Alicia"] },
                 { "value": "Bob" },
                 { "value": "Carol" }
            ] }
            """);
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

    private HttpClient Client(string suffix = "alice")
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Test-User", _userPrefix + suffix);
        return c;
    }

    private async Task<SessionDto> NewSession(HttpClient client)
        => (await (await client.PostAsJsonAsync("/api/sessions",
            new CreateSessionRequest("a", 3))).Content.ReadFromJsonAsync<SessionDto>())!;

    [Fact]
    public async Task GET_next_pair_returns_two_distinct_items()
    {
        var client = Client();
        var session = await NewSession(client);

        var res = await client.GetAsync($"/api/sessions/{session.Id}/next-pair");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var pair = await res.Content.ReadFromJsonAsync<PairDto>();
        pair!.A.Value.Should().NotBe(pair.B.Value);
    }

    [Fact]
    public async Task POST_vote_records_and_updates_state()
    {
        var client = Client();
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
        var client = Client();
        var session = await NewSession(client);

        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/votes",
            new VoteRequest("Alice", "Bob", "nuke"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_vote_on_other_users_session_returns_404()
    {
        var alice = Client("alice");
        var session = await NewSession(alice);

        var bob = Client("bob");
        var res = await bob.PostAsJsonAsync($"/api/sessions/{session.Id}/votes",
            new VoteRequest("Alice", "Bob", "prefer_a"));

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_results_returns_ranked_items()
    {
        var client = Client();
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
