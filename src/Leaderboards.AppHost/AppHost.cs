var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var matchesDb = postgres.AddDatabase("matches-db");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();

var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator(emulator => emulator
        .WithImageRegistry("mcr.microsoft.com")
        .WithImage("cosmosdb/linux/azure-cosmos-emulator")
        .WithImageTag("vnext-preview"));

var serviceBus = builder.AddAzureServiceBus("service-bus")
    .RunAsEmulator();

serviceBus.AddServiceBusQueue("match-created");

var matchesApi = builder.AddProject<Projects.Leaderboards_MatchesApi>("matches-api")
    .WithReference(matchesDb)
    .WithReference(serviceBus)
    .WaitFor(matchesDb)
    .WaitFor(serviceBus)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Leaderboards_LeaderboardsApi>("leaderboards-api")
    .WithReference(cosmos)
    .WithHttpHealthCheck("/health");

builder.AddAzureFunctionsProject<Projects.Leaderboards_Service>("leaderboards-service")
    .WithReference(serviceBus)
    .WithReference(matchesApi)
    .WithReference(cosmos)
    .WithHostStorage(storage)
    .WaitFor(serviceBus)
    .WaitFor(matchesApi);

builder.Build().Run();
