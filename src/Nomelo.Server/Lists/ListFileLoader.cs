using System.Text.Json;

namespace Nomelo.Server.Lists;

public class ListFileLoader
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ListFile Load(string path)
    {
        using var stream = File.OpenRead(path);
        var raw = JsonSerializer.Deserialize<RawList>(stream, Json)
                  ?? throw new ListFileException($"empty or invalid JSON: {path}");
        return Build(raw, path);
    }

    public async Task<ListFile> LoadAsync(string path, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        var raw = await JsonSerializer.DeserializeAsync<RawList>(stream, Json, ct)
                  ?? throw new ListFileException($"empty or invalid JSON: {path}");
        return Build(raw, path);
    }

    private static ListFile Build(RawList raw, string path)
    {
        if (string.IsNullOrWhiteSpace(raw.Id))
            throw new ListFileException($"missing 'id' in {path}");
        if (string.IsNullOrWhiteSpace(raw.Name))
            throw new ListFileException($"missing 'name' in {path}");
        if (raw.Items is null || raw.Items.Count == 0)
            throw new ListFileException($"empty 'items' in {path}");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<ListFileItem>(raw.Items.Count);
        foreach (var i in raw.Items)
        {
            if (string.IsNullOrWhiteSpace(i.Value))
                throw new ListFileException($"item missing 'value' in {path}");
            if (!seen.Add(i.Value))
                throw new ListFileException($"duplicate value '{i.Value}' in {path}");
            items.Add(new ListFileItem(i.Value, (IReadOnlyList<string>?)i.Variants ?? Array.Empty<string>(), i.Description));
        }

        return new ListFile(raw.Id, raw.Name, items);
    }

    private sealed class RawList
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<RawItem>? Items { get; set; }
    }

    private sealed class RawItem
    {
        public string? Value { get; set; }
        public List<string>? Variants { get; set; }
        public string? Description { get; set; }
    }
}
