using Leaderboards.Contracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Leaderboards.Service;

public class MatchCreatedFunction(ILogger<MatchCreatedFunction> logger)
{
    [Function(nameof(MatchCreatedFunction))]
    public void Run(
        [ServiceBusTrigger("match-created", Connection = "service-bus")] MatchCreatedMessage matchCreated)
    {
        logger.LogInformation(
            "Match created — Venue: {VenueName}, At: {OccurredAtUtc}",
            matchCreated.VenueName, matchCreated.OccurredAtUtc);
    }
}
