using Azure.Messaging.ServiceBus;
using Leaderboards.Contracts;
using Leaderboards.MatchesApi.Data;

namespace Leaderboards.MatchesApi.Matches.Create;

public record CreateMatchRequest(string WinnerId, string LoserId, string VenueName);
public record CreateMatchResponse(Guid Id, string WinnerId, string LoserId, string VenueName);

public static class CreateMatchEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/matches", async (CreateMatchRequest request, MatchesDbContext db, ServiceBusSender sender) =>
        {
            var match = new Match
            {
                Id = Guid.NewGuid(),
                WinnerId = request.WinnerId,
                LoserId = request.LoserId,
                VenueName = request.VenueName,
            };

            db.Matches.Add(match);
            await db.SaveChangesAsync();

            var message = new ServiceBusMessage(
                BinaryData.FromObjectAsJson(new MatchCreatedMessage(DateTime.UtcNow, match.VenueName)))
            {
                ContentType = "application/json"
            };

            await sender.SendMessageAsync(message);

            return Results.Created(
                $"/matches/{match.Id}",
                new CreateMatchResponse(match.Id, match.WinnerId, match.LoserId, match.VenueName));
        })
        .WithName("CreateMatch")
        .WithSummary("Submit a match result")
        .Produces<CreateMatchResponse>(StatusCodes.Status201Created);
    }
}
