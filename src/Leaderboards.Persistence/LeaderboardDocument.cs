namespace Leaderboards.Persistence;

public record LeaderboardDocument(
    string id,           // Cosmos DB id — equals VenueName
    string VenueName,
    List<LeaderboardEntry> Entries,   // ordered by Rank ascending (Rank 1 first)
    DateTime UpdatedAtUtc);
