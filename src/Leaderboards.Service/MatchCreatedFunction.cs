using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Leaderboards.Contracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Leaderboards.Service;

public class MatchCreatedFunction(ILogger<MatchCreatedFunction> logger, MatchesApiClient matchesApi)
{
    private static readonly ActivitySource ActivitySource = new("Leaderboards.Service");

    [Function(nameof(MatchCreatedFunction))]
    public async Task Run(
        [ServiceBusTrigger("match-created", Connection = "service-bus")] ServiceBusReceivedMessage message)
    {
        var matchCreated = message.Body.ToObjectFromJson<MatchCreatedMessage>()!;

        // Restore the distributed trace context propagated via the Diagnostic-Id message property.
        var diagnosticId = message.ApplicationProperties.GetValueOrDefault("Diagnostic-Id")?.ToString();
        ActivityContext parentContext = default;
        if (diagnosticId is not null)
            ActivityContext.TryParse(diagnosticId, traceState: null, isRemote: true, out parentContext);

        using var activity = ActivitySource.StartActivity("match-created process", ActivityKind.Consumer, parentContext);

        logger.LogInformation(
            "Match created — Venue: {VenueName}, At: {OccurredAtUtc}",
            matchCreated.VenueName, matchCreated.OccurredAtUtc);

        var matches = await matchesApi.GetMatchesForVenueAsync(matchCreated.VenueName);

        var leaderboard = matches
            .SelectMany(m => new[] { m.WinnerId, m.LoserId })
            .Distinct()
            .Select(playerId => new { PlayerId = playerId, Skill = Random.Shared.Next(1, 101) })
            .OrderByDescending(e => e.Skill)
            .ToList();

        logger.LogInformation(
            "Leaderboard for {VenueName} ({Count} players):",
            matchCreated.VenueName, leaderboard.Count);

        foreach (var (entry, rank) in leaderboard.Select((e, i) => (e, i + 1)))
            logger.LogInformation("  #{Rank} {PlayerId} — Skill: {Skill}", rank, entry.PlayerId, entry.Skill);
    }
}
