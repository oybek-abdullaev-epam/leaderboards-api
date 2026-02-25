using System.Net;
using Microsoft.Azure.Cosmos;

namespace Leaderboards.Persistence;

public class LeaderboardRepository(CosmosClient cosmosClient)
{
    private const string DatabaseId = "leaderboards-db";
    private const string ContainerId = "leaderboards";

    public async Task UpsertAsync(LeaderboardDocument document, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var container = cosmosClient.GetDatabase(DatabaseId).GetContainer(ContainerId);
        await container.UpsertItemAsync(document, new PartitionKey(document.VenueName), cancellationToken: ct);
    }

    public async Task<LeaderboardDocument?> GetAsync(string venueName, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var container = cosmosClient.GetDatabase(DatabaseId).GetContainer(ContainerId);
        try
        {
            var response = await container.ReadItemAsync<LeaderboardDocument>(
                id: venueName,
                partitionKey: new PartitionKey(venueName),
                cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        var dbResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId, cancellationToken: ct);
        await dbResponse.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ContainerId, "/VenueName"), cancellationToken: ct);
    }
}
