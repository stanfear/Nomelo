namespace NameSelect.Server.Data.Entities;

public class VotingSession
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string ListId { get; set; } = "";
    public int ConfidenceThreshold { get; set; } = 3;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? ShareToken { get; set; }
}
