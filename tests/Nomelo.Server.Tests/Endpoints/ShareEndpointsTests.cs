using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nomelo.Server.Tests.Infrastructure;
using Nomelo.Shared.Dtos;
using Xunit;

namespace Nomelo.Server.Tests.Endpoints;

[Collection("postgres")]
public class ShareEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private NomeloAppFactory _factory = default!;
    private string _listsDir = "";
    private readonly string _userPrefix = $"u-{Guid.NewGuid():N}-";

    public ShareEndpointsTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync()
    {
        _listsDir = Path.Combine(Path.GetTempPath(), $"nsshare-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_listsDir);
        File.WriteAllText(Path.Combine(_listsDir, "a.json"),
            """
            { "id": "a", "name": "A", "items": [{ "value": "x" }, { "value": "y" }] }
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

    [Fact]
    public async Task GET_share_with_valid_token_returns_results_without_auth()
    {
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Add("X-Test-User", _userPrefix + "alice");
        var session = await (await owner.PostAsJsonAsync("/api/sessions",
            new CreateSessionRequest("a", 3))).Content.ReadFromJsonAsync<SessionDto>();

        var anon = _factory.CreateClient();
        // no X-Test-User header → TestAuthHandler still authenticates as default,
        // but the endpoint must not require ownership

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
