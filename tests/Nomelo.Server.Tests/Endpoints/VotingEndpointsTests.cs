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
    public async Task POST_items_bans_marks_existing_states_banned()
    {
        var client = Client();
        var session = await NewSession(client);
        // Touch Alice via a vote so she has an existing ItemState row.
        await client.PostAsJsonAsync($"/api/sessions/{session.Id}/votes",
            new VoteRequest("Alice", "Bob", "prefer_a"));

        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Alice" }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var results = await client.GetFromJsonAsync<ResultsDto>($"/api/sessions/{session.Id}/results");
        results!.Banned.Should().ContainSingle(b => b.Value == "Alice");
    }

    [Fact]
    public async Task POST_items_bans_creates_state_for_untouched_items()
    {
        var client = Client();
        var session = await NewSession(client);

        // Carol has never appeared in a vote; the endpoint must still create
        // an ItemState row with IsBanned=true so /results sees her as banned.
        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Carol" }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var results = await client.GetFromJsonAsync<ResultsDto>($"/api/sessions/{session.Id}/results");
        results!.Banned.Should().ContainSingle(b => b.Value == "Carol");
        results.Ranked.Should().NotContain(r => r.Value == "Carol");
    }

    [Fact]
    public async Task POST_items_bans_handles_mixed_existing_and_new_items()
    {
        var client = Client();
        var session = await NewSession(client);
        await client.PostAsJsonAsync($"/api/sessions/{session.Id}/votes",
            new VoteRequest("Alice", "Bob", "prefer_a"));

        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Alice", "Carol" }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var results = await client.GetFromJsonAsync<ResultsDto>($"/api/sessions/{session.Id}/results");
        results!.Banned.Select(b => b.Value).Should().BeEquivalentTo(new[] { "Alice", "Carol" });
    }

    [Fact]
    public async Task POST_items_bans_is_idempotent()
    {
        var client = Client();
        var session = await NewSession(client);

        await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Alice" }));
        var second = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Alice", "Alice" }));

        second.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var results = await client.GetFromJsonAsync<ResultsDto>($"/api/sessions/{session.Id}/results");
        results!.Banned.Where(b => b.Value == "Alice").Should().ContainSingle();
    }

    [Fact]
    public async Task POST_items_bans_rejects_unknown_items()
    {
        var client = Client();
        var session = await NewSession(client);

        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Alice", "Zorro" }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Reject is atomic: nothing should be banned when the input contains
        // an unknown item, including the valid ones in the same batch.
        var results = await client.GetFromJsonAsync<ResultsDto>($"/api/sessions/{session.Id}/results");
        results!.Banned.Should().BeEmpty();
    }

    [Fact]
    public async Task POST_items_bans_rejects_empty_list()
    {
        var client = Client();
        var session = await NewSession(client);

        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(Array.Empty<string>()));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_items_bans_on_other_users_session_returns_404()
    {
        var alice = Client("alice");
        var session = await NewSession(alice);

        var bob = Client("bob");
        var res = await bob.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Alice" }));

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_items_unbans_restores_banned_items()
    {
        var client = Client();
        var session = await NewSession(client);
        await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Alice", "Bob" }));

        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/unbans",
            new BulkBanRequest(new[] { "Alice" }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var results = await client.GetFromJsonAsync<ResultsDto>($"/api/sessions/{session.Id}/results");
        results!.Banned.Select(b => b.Value).Should().BeEquivalentTo(new[] { "Bob" });
        results.Ranked.Should().Contain(r => r.Value == "Alice");
    }

    [Fact]
    public async Task POST_items_unbans_is_idempotent_on_non_banned_items()
    {
        var client = Client();
        var session = await NewSession(client);

        // Nobody is banned; unban should still succeed (no-op).
        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/unbans",
            new BulkBanRequest(new[] { "Alice", "Bob" }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var results = await client.GetFromJsonAsync<ResultsDto>($"/api/sessions/{session.Id}/results");
        results!.Banned.Should().BeEmpty();
    }

    [Fact]
    public async Task POST_items_unbans_rejects_empty_list()
    {
        var client = Client();
        var session = await NewSession(client);

        var res = await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/unbans",
            new BulkBanRequest(Array.Empty<string>()));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_items_unbans_on_other_users_session_returns_404()
    {
        var alice = Client("alice");
        var session = await NewSession(alice);
        await alice.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Alice" }));

        var bob = Client("bob");
        var res = await bob.PostAsJsonAsync($"/api/sessions/{session.Id}/items/unbans",
            new BulkBanRequest(new[] { "Alice" }));

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

    [Fact]
    public async Task POST_export_unbanned_returns_filtered_json_attachment()
    {
        var client = Client();
        var session = await NewSession(client);
        await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Bob" }));

        var res = await client.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/export-unbanned",
            new ExportUnbannedRequest("a-filtered", "A (filtré)"));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        res.Content.Headers.ContentDisposition?.FileName.Should().Contain("a-filtered.json");

        using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("id").GetString().Should().Be("a-filtered");
        doc.RootElement.GetProperty("name").GetString().Should().Be("A (filtré)");
        var values = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("value").GetString())
            .ToArray();
        values.Should().BeEquivalentTo(new[] { "Alice", "Carol" });
    }

    [Fact]
    public async Task POST_export_unbanned_rejects_invalid_slug()
    {
        var client = Client();
        var session = await NewSession(client);

        var res = await client.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/export-unbanned",
            new ExportUnbannedRequest("Bad Id!", "x"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_export_unbanned_rejects_collision_with_existing_list()
    {
        var client = Client();
        var session = await NewSession(client);

        // The source list is registered with id "a"; reusing it must be refused
        // so a later drop into lists/ doesn't silently overwrite the source.
        var res = await client.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/export-unbanned",
            new ExportUnbannedRequest("a", "A copy"));

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_export_unbanned_rejects_when_no_items_remain()
    {
        var client = Client();
        var session = await NewSession(client);
        await client.PostAsJsonAsync($"/api/sessions/{session.Id}/items/bans",
            new BulkBanRequest(new[] { "Alice", "Bob", "Carol" }));

        var res = await client.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/export-unbanned",
            new ExportUnbannedRequest("a-empty", "Empty"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_export_unbanned_rejects_non_owner()
    {
        var alice = Client("alice");
        var session = await NewSession(alice);

        var bob = Client("bob");
        var res = await bob.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/export-unbanned",
            new ExportUnbannedRequest("a-filtered", "x"));

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
