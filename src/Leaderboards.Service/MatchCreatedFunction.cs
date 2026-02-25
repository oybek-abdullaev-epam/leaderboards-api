using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Leaderboards.Contracts;
using Leaderboards.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Leaderboards.Service;

public class MatchCreatedFunction(
    ILogger<MatchCreatedFunction> logger,
    MatchesApiClient matchesApi,
    LeaderboardBuilder leaderboardBuilder,
    LeaderboardRepository leaderboardRepository)
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
        var entries = leaderboardBuilder.Build(matches);

        var doc = new LeaderboardDocument(
            id: matchCreated.VenueName,
            VenueName: matchCreated.VenueName,
            Entries: entries,
            UpdatedAtUtc: DateTime.UtcNow);

        await leaderboardRepository.UpsertAsync(doc);

        logger.LogInformation(
            "Leaderboard for {VenueName} written to CosmosDB ({Count} players)",
            matchCreated.VenueName, entries.Count);
    }
}
