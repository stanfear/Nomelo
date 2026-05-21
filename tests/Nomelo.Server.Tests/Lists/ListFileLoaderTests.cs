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
    public void Load_populates_sparkline_metadata_when_present()
    {
        var path = WriteTemp("""
        {
          "id": "prenoms-fr",
          "name": "Prénoms",
          "sparklineMeta": { "buckets": 25, "bucketSize": 5, "startYear": 1900, "endYear": 2024 },
          "items": [
            {
              "value": "Catherine",
              "sparkline": "▆▆▇███▇▆▅▅▄▄",
              "peakYear": 1963,
              "peakCount": 95245
            }
          ]
        }
        """);
        var loader = new ListFileLoader();

        var result = loader.Load(path);

        result.Items.Should().HaveCount(1);
        result.Items[0].Sparkline.Should().Be("▆▆▇███▇▆▅▅▄▄");
        result.Items[0].PeakYear.Should().Be(1963);
        result.Items[0].PeakCount.Should().Be(95245);
    }

    [Fact]
    public void Load_leaves_sparkline_metadata_null_when_absent()
    {
        var path = WriteTemp("""
        { "id": "x", "name": "x", "items": [{ "value": "a" }] }
        """);
        var loader = new ListFileLoader();

        var result = loader.Load(path);

        result.Items[0].Sparkline.Should().BeNull();
        result.Items[0].PeakYear.Should().BeNull();
        result.Items[0].PeakCount.Should().BeNull();
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
