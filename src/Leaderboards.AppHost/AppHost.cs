var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var matchesDb = postgres.AddDatabase("matchesdb");

builder.AddProject<Projects.Leaderboards_MatchesApi>("matches-api")
    .WithReference(matchesDb)
    .WaitFor(matchesDb)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
