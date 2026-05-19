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
        result.Select(r => r.List!.Id).Should().BeEquivalentTo(new[] { "a", "b" });
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
        result.Single(r => r.Error is null).List!.Id.Should().Be("a");
    }

    [Fact]
    public void Scan_missing_directory_returns_empty()
    {
        var scanner = new ListDirectoryScanner(new ListFileLoader());

        var result = scanner.Scan(Path.Combine(_dir, "does-not-exist")).ToList();

        result.Should().BeEmpty();
    }
}
