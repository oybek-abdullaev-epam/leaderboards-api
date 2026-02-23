using Leaderboards.MatchesApi.Data;
using Microsoft.EntityFrameworkCore;

namespace Leaderboards.MatchesApi.Matches.GetAll;

public record MatchResponse(Guid Id, string WinnerId, string LoserId, string VenueName);

public static class GetMatchesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/matches", async (string? venueName, MatchesDbContext db) =>
        {
            var query = db.Matches.AsNoTracking();

            if (venueName is not null)
                query = query.Where(m => m.VenueName == venueName);

            var matches = await query
                .Select(m => new MatchResponse(m.Id, m.WinnerId, m.LoserId, m.VenueName))
                .ToListAsync();

            return Results.Ok(matches);
        })
        .WithName("GetMatches")
        .WithSummary("Get matches, optionally filtered by venue")
        .Produces<List<MatchResponse>>();
    }
}
