using FluentAssertions;
using NameSelect.Server.Lists;

namespace NameSelect.Server.Tests.Lists;

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
