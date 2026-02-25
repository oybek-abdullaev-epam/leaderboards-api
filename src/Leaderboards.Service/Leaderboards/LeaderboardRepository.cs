using System.Net;
using Microsoft.Azure.Cosmos;

namespace Leaderboards.Service;

public class LeaderboardRepository(CosmosClient cosmosClient)
{
    private const string DatabaseId = "leaderboards-db";
    private const string ContainerId = "leaderboards";

    // Volatile bool is sufficient: a false positive (two concurrent first calls) just means
    // two harmless 409-caught create attempts; after that it's always true.
    private volatile bool _initialized;

    public async Task UpsertAsync(LeaderboardDocument document, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var container = cosmosClient.GetDatabase(DatabaseId).GetContainer(ContainerId);
        await container.UpsertItemAsync(document, new PartitionKey(document.VenueName), cancellationToken: ct);
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        try { await cosmosClient.CreateDatabaseAsync(DatabaseId, cancellationToken: ct); }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict) { }

        var db = cosmosClient.GetDatabase(DatabaseId);
        try { await db.CreateContainerAsync(new ContainerProperties(ContainerId, "/VenueName"), cancellationToken: ct); }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict) { }

        _initialized = true;
    }
}
