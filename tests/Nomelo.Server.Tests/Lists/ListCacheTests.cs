using FluentAssertions;
using Nomelo.Server.Lists;

namespace Nomelo.Server.Tests.Lists;

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
