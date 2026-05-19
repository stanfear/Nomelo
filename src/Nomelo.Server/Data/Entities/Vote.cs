namespace Nomelo.Server.Data.Entities;

public enum VoteResult
{
    PreferA,
    PreferB,
    BanA,
    BanB,
    BanBoth,
    LikeBoth
}

public class Vote
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string ItemA { get; set; } = "";
    public string ItemB { get; set; } = "";
    public VoteResult Result { get; set; }
    public DateTimeOffset PresentedAt { get; set; }
}
