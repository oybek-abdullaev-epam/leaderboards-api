var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var matchesDb = postgres.AddDatabase("matches-db");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();

var serviceBus = builder.AddAzureServiceBus("service-bus")
    .RunAsEmulator();

serviceBus.AddServiceBusQueue("match-created");

var matchesApi = builder.AddProject<Projects.Leaderboards_MatchesApi>("matches-api")
    .WithReference(matchesDb)
    .WithReference(serviceBus)
    .WaitFor(matchesDb)
    .WaitFor(serviceBus)
    .WithHttpHealthCheck("/health");

builder.AddAzureFunctionsProject<Projects.Leaderboards_Service>("leaderboards-service")
    .WithReference(serviceBus)
    .WithReference(matchesApi)
    .WithHostStorage(storage)
    .WaitFor(serviceBus)
    .WaitFor(matchesApi);

builder.Build().Run();
