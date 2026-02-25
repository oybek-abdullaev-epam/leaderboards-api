using Leaderboards.Contracts;

namespace Leaderboards.Service;

public class LeaderboardBuilder
{
    public List<LeaderboardEntry> Build(IEnumerable<MatchResponse> matches) =>
        matches
            .SelectMany(m => new[] { m.WinnerId, m.LoserId })
            .Distinct()
            .Select(playerId => (PlayerId: playerId, Skill: Random.Shared.Next(1, 101)))
            .OrderByDescending(e => e.Skill)
            .Select((e, i) => new LeaderboardEntry(Rank: i + 1, e.PlayerId, e.Skill))
            .ToList();
}
