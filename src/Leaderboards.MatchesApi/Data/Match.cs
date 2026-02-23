namespace Leaderboards.MatchesApi.Data;

public class Match
{
    public Guid Id { get; set; }
    public string WinnerId { get; set; } = string.Empty;
    public string LoserId { get; set; } = string.Empty;
}
