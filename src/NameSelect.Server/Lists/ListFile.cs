namespace NameSelect.Server.Lists;

public record ListFileItem(string Value, IReadOnlyList<string> Variants, string? Description);

public record ListFile(string Id, string Name, IReadOnlyList<ListFileItem> Items);

public class ListFileException : Exception
{
    public ListFileException(string message) : base(message) { }
}
