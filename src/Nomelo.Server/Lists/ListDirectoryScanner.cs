namespace Nomelo.Server.Lists;

public record ScanResult(string Path, ListFile? List, string? Error);

public class ListDirectoryScanner
{
    private readonly ListFileLoader _loader;

    public ListDirectoryScanner(ListFileLoader loader) => _loader = loader;

    public IEnumerable<ScanResult> Scan(string directory)
    {
        if (!Directory.Exists(directory)) yield break;

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            ListFile? list = null;
            string? error = null;
            try { list = _loader.Load(path); }
            catch (Exception ex) { error = ex.Message; }
            yield return new ScanResult(path, list, error);
        }
    }
}
