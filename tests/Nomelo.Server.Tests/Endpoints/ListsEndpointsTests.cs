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
