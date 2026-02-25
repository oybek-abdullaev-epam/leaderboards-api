using Leaderboards.Persistence;

namespace Leaderboards.LeaderboardsApi.Leaderboards.Get;

public record LeaderboardEntryResponse(int Rank, string PlayerId, int Skill);
public record GetLeaderboardResponse(string VenueName, List<LeaderboardEntryResponse> Entries, DateTime UpdatedAtUtc);

public static class GetLeaderboardEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/leaderboards/{venueName}", async (string venueName, LeaderboardRepository repository, CancellationToken ct) =>
        {
            var document = await repository.GetAsync(venueName, ct);
            if (document is null)
                return Results.NotFound();

            var response = new GetLeaderboardResponse(
                VenueName: document.VenueName,
                Entries: document.Entries
                    .Select(e => new LeaderboardEntryResponse(e.Rank, e.PlayerId, e.Skill))
                    .ToList(),
                UpdatedAtUtc: document.UpdatedAtUtc);

            return Results.Ok(response);
        })
        .WithName("GetLeaderboard")
        .WithSummary("Get the ranked leaderboard for a venue")
        .Produces<GetLeaderboardResponse>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
