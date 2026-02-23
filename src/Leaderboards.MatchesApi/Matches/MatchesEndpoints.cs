using Leaderboards.MatchesApi.Matches.Create;
using Leaderboards.MatchesApi.Matches.GetAll;

namespace Leaderboards.MatchesApi.Matches;

public static class MatchesEndpoints
{
    public static WebApplication MapMatchesEndpoints(this WebApplication app)
    {
        CreateMatchEndpoint.Map(app);
        GetMatchesEndpoint.Map(app);
        return app;
    }
}
