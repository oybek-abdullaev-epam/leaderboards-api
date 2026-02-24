var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var matchesDb = postgres.AddDatabase("matches-db");

var serviceBus = builder.AddAzureServiceBus("service-bus")
    .RunAsEmulator();

serviceBus.AddServiceBusQueue("match-created");

builder.AddProject<Projects.Leaderboards_MatchesApi>("matches-api")
    .WithReference(matchesDb)
    .WithReference(serviceBus)
    .WaitFor(matchesDb)
    .WaitFor(serviceBus)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
