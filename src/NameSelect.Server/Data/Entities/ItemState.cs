namespace NameSelect.Server.Data.Entities;

public class ItemState
{
    public Guid SessionId { get; set; }
    public string Item { get; set; } = "";
    public double EloScore { get; set; } = 1000.0;
    public int TimesShown { get; set; }
    public bool IsBanned { get; set; }
}
