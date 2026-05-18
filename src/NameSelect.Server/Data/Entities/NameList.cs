namespace NameSelect.Server.Data.Entities;

public class NameList
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int ItemCount { get; set; }
    public DateTimeOffset LoadedAt { get; set; }
}
