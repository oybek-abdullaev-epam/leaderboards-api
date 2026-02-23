using Leaderboards.MatchesApi.Data;
using Microsoft.EntityFrameworkCore;

namespace Leaderboards.MatchesApi.Matches.GetAll;

public record MatchResponse(Guid Id, string WinnerId, string LoserId);

public static class GetMatchesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/matches", async (MatchesDbContext db) =>
        {
            var matches = await db.Matches
                .AsNoTracking()
                .Select(m => new MatchResponse(m.Id, m.WinnerId, m.LoserId))
                .ToListAsync();

            return Results.Ok(matches);
        })
        .WithName("GetMatches")
        .WithSummary("Get all matches")
        .Produces<List<MatchResponse>>();
    }
}
