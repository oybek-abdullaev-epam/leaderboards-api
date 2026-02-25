using Leaderboards.LeaderboardsApi.Leaderboards.Get;

namespace Leaderboards.LeaderboardsApi.Leaderboards;

public static class LeaderboardsEndpoints
{
    public static WebApplication MapLeaderboardsEndpoints(this WebApplication app)
    {
        GetLeaderboardEndpoint.Map(app);
        return app;
    }
}
