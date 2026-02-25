using Leaderboards.LeaderboardsApi.Leaderboards;
using Leaderboards.Persistence;
using Leaderboards.ServiceDefaults;

AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.AddLeaderboardsPersistence();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "Leaderboards API is running.");
app.MapLeaderboardsEndpoints();
app.MapDefaultEndpoints();

app.Run();
