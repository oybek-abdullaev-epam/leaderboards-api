var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Leaderboards_MatchesApi>("matches-api")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
