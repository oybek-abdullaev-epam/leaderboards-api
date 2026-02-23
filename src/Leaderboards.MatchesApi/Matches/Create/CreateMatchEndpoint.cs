using Leaderboards.MatchesApi.Data;

namespace Leaderboards.MatchesApi.Matches.Create;

public record CreateMatchRequest(string WinnerId, string LoserId);
public record CreateMatchResponse(Guid Id, string WinnerId, string LoserId);

public static class CreateMatchEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/matches", async (CreateMatchRequest request, MatchesDbContext db) =>
        {
            var match = new Match
            {
                Id = Guid.NewGuid(),
                WinnerId = request.WinnerId,
                LoserId = request.LoserId,
            };

            db.Matches.Add(match);
            await db.SaveChangesAsync();

            return Results.Created(
                $"/matches/{match.Id}",
                new CreateMatchResponse(match.Id, match.WinnerId, match.LoserId));
        })
        .WithName("CreateMatch")
        .WithSummary("Submit a match result")
        .Produces<CreateMatchResponse>(StatusCodes.Status201Created);
    }
}
