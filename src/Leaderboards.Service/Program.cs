using Leaderboards.Persistence;
using Leaderboards.Service;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var builder = FunctionsApplication.CreateBuilder(args);

// Aspire injects Service Bus as ConnectionStrings:service-bus, but the Functions isolated
// worker trigger binding looks for service-bus__connectionString (new-style SDK format).
var serviceBusConn = builder.Configuration.GetConnectionString("service-bus");
if (!string.IsNullOrEmpty(serviceBusConn))
    builder.Configuration["service-bus__connectionString"] = serviceBusConn;

builder.Services.AddServiceDiscovery();

builder.Services.AddHttpClient<MatchesApiClient>(c =>
    c.BaseAddress = new Uri("https://matches-api"))
    .AddServiceDiscovery();

builder.AddLeaderboardsPersistence();
builder.Services.AddSingleton<LeaderboardBuilder>();

// Add tracing only (not logging — logs flow through the host to avoid duplicates in Aspire).
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Leaderboards.Service")
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

builder.Build().Run();
