using System.Net.Http.Json;
using FluentAssertions;
using Nomelo.Server.Tests.Infrastructure;
using Nomelo.Shared.Dtos;
using Xunit;

namespace Nomelo.Server.Tests.Endpoints;

[Collection("postgres")]
public class EndToEndSmokeTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private NomeloAppFactory _factory = default!;
    private string _listsDir = "";
    private readonly string _userPrefix = $"u-{Guid.NewGuid():N}-";

    public EndToEndSmokeTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync()
    {
        _listsDir = Path.Combine(Path.GetTempPath(), $"nse2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_listsDir);
        var items = string.Join(",", Enumerable.Range(0, 20)
            .Select(i => $"{{ \"value\": \"n{i}\" }}"));
        File.WriteAllText(Path.Combine(_listsDir, "a.json"),
            $"{{ \"id\": \"a\", \"name\": \"A\", \"items\": [{items}] }}");

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
    public async Task Favourite_item_rises_to_top_when_consistently_preferred()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", _userPrefix + "alice");

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
