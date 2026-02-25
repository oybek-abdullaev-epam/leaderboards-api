namespace Leaderboards.Service;

public record LeaderboardEntry(int Rank, string PlayerId, int Skill);

public record LeaderboardDocument(
    string id,           // Cosmos DB id — equals VenueName
    string VenueName,
    List<LeaderboardEntry> Entries,   // ordered by Rank ascending (Rank 1 first)
    DateTime UpdatedAtUtc);
