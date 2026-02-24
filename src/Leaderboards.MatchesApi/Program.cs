using Azure.Messaging.ServiceBus;
using Leaderboards.MatchesApi.Data;
using Leaderboards.MatchesApi.Matches;
using Leaderboards.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.AddNpgsqlDbContext<MatchesDbContext>("matches-db");

builder.AddAzureServiceBusClient("service-bus");
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<ServiceBusClient>().CreateSender("match-created"));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MatchesDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/", () => "Matches api is running.");
app.MapMatchesEndpoints();
app.MapDefaultEndpoints();

app.Run();
