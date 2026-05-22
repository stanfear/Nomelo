namespace Nomelo.Server.Lists;

public record ListFileItem(
    string Value,
    IReadOnlyList<string> Variants,
    string? Description,
    string? Sparkline,
    int? PeakYear,
    int? PeakCount);

public record ListFile(string Id, string Name, IReadOnlyList<ListFileItem> Items);

public class ListFileException : Exception
{
    public ListFileException(string message) : base(message) { }
}
