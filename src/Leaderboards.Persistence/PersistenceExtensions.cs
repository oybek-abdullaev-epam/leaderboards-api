using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Leaderboards.Persistence;

public static class PersistenceExtensions
{
    public static void AddLeaderboardsPersistence(this IHostApplicationBuilder builder)
    {
        // Aspire injects the CosmosDB connection string differently per project type:
        //   Functions projects: Aspire:Microsoft:Azure:Cosmos:cosmos:ConnectionString
        //   Standard ASP.NET Core projects: ConnectionStrings:cosmos
        // The vnext-preview emulator serves plain HTTP, so rewrite tcp:// -> http://.
        var cosmosConn = (builder.Configuration["Aspire:Microsoft:Azure:Cosmos:cosmos:ConnectionString"]
            ?? builder.Configuration.GetConnectionString("cosmos"))
            ?.Replace("tcp://", "http://")
            ?? throw new InvalidOperationException("CosmosDB connection string not found. Expected 'Aspire:Microsoft:Azure:Cosmos:cosmos:ConnectionString' or 'ConnectionStrings:cosmos'.");

        // Register CosmosClient directly rather than via AddAzureCosmosClient. The emulator
        // connection string contains AccountKey, so this uses key-based auth exclusively.
        // Bypassing Aspire's Azure client factory avoids DefaultAzureCredential being attempted
        // through the StandardResilienceHandler, which would fail on IMDS in local development.
        builder.Services.AddSingleton(_ => CreateCosmosClient(cosmosConn));

        builder.Services.AddSingleton<LeaderboardRepository>();
    }

    private static CosmosClient CreateCosmosClient(string connectionString)
    {
        var options = new CosmosClientOptions
        {
            // The vnext-preview emulator serves plain HTTP; Gateway mode is required for the emulator.
            ConnectionMode = ConnectionMode.Gateway,
            // The emulator's discovery response returns its internal Docker address (127.0.0.1:8081)
            // as writableLocations/readableLocations. Without this flag the SDK would follow that
            // redirect and fail to connect. LimitToEndpoint forces all requests through the
            // provided endpoint regardless of what the discovery response says.
            LimitToEndpoint = true
        };

        // Parse the connection string explicitly to extract AccountEndpoint and AccountKey.
        // When AccountKey is present, use CosmosClient(endpoint, key, options) — this constructor
        // uses key-based auth with absolutely no DefaultAzureCredential / IMDS fallback.
        var parts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = segment.IndexOf('=');
            if (idx > 0)
                parts[segment[..idx].Trim()] = segment[(idx + 1)..].Trim();
        }

        if (parts.TryGetValue("AccountEndpoint", out var endpoint) &&
            parts.TryGetValue("AccountKey", out var accountKey))
        {
            return new CosmosClient(endpoint, accountKey, options);
        }

        // No AccountKey found — full connection string as-is (production with managed identity).
        return new CosmosClient(connectionString, options);
    }
}
