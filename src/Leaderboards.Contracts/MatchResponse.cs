namespace Leaderboards.Contracts;

public record MatchResponse(Guid Id, string WinnerId, string LoserId, string VenueName);
