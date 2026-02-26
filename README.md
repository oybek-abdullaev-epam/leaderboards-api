# Leaderboards API

A distributed leaderboard system built with .NET 10, ASP.NET Core, and Azure services. Match results are recorded via a REST API, processed asynchronously through Azure Service Bus, and ranked leaderboards are served from CosmosDB.

## Architecture

```
POST /matches  →  MatchesApi  →  Service Bus  →  Leaderboards.Service  →  CosmosDB
                                                                               ↑
GET /leaderboards/{venue}  →  LeaderboardsApi  →──────────────────────────────┘
```

The system is orchestrated locally via [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/), which provisions all infrastructure (PostgreSQL, Azure Service Bus emulator, CosmosDB emulator) and wires up service discovery automatically.

### Projects

| Project | Type | Description |
|---------|------|-------------|
| `Leaderboards.MatchesApi` | ASP.NET Core API | Records match results. Persists to PostgreSQL and publishes `MatchCreatedMessage` to Service Bus. |
| `Leaderboards.LeaderboardsApi` | ASP.NET Core API | Reads pre-computed leaderboards from CosmosDB. |
| `Leaderboards.Service` | Azure Functions (isolated) | Subscribes to `match-created`, computes rankings via `LeaderboardBuilder`, and upserts to CosmosDB. |
| `Leaderboards.Persistence` | Class Library | Shared CosmosDB types and `LeaderboardRepository`. |
| `Leaderboards.Contracts` | Class Library | Shared message/response types (`MatchCreatedMessage`, `MatchResponse`). |
| `Leaderboards.AppHost` | Aspire Host | Local orchestration — start this for development. |
| `Leaderboards.ServiceDefaults` | Class Library | OpenTelemetry, resilience handlers, and health endpoints. |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`
- [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local) (`func`) — must be on `PATH`
- Docker (for infrastructure emulators)

## Getting Started

**1. Trust the HTTPS dev certificate (first time only):**
```bash
dotnet dev-certs https --trust
```

**2. Build the solution:**
```bash
dotnet build src/Leaderboards.slnx
```

**3. Run with Aspire (starts all services and infrastructure):**
```bash
dotnet run --project src/Leaderboards.AppHost
```

The Aspire dashboard will print its URL on startup. Open it to see all running services, logs, and traces.

## API Reference

### Matches API — `https://localhost:7335`

**Record a match result:**
```http
POST /matches
Content-Type: application/json

{
  "winnerId": "player-1",
  "loserId": "player-2",
  "venueName": "arena-1"
}
```
Returns `201 Created`.

**List matches:**
```http
GET /matches?venueName=arena-1
```
Returns `200 OK` with an array of `MatchResponse`. The `venueName` filter is optional.

### Leaderboards API — `https://localhost:7336`

**Get leaderboard for a venue:**
```http
GET /leaderboards/arena-1
```
Returns `200 OK` with a ranked leaderboard, or `404 Not Found` if no matches have been processed yet.

> `.http` test files are available at `src/Leaderboards.MatchesApi/MatchesApi.http` and `src/Leaderboards.LeaderboardsApi/LeaderboardsApi.http`.

OpenAPI schema is served at `/openapi/v1.json` in the Development environment.

## Database Migrations

EF Core migrations are applied automatically on startup in the Development environment. To add a new migration:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Leaderboards.MatchesApi \
  --output-dir Data/Migrations
```

## Observability

All services export traces and metrics via OpenTelemetry to the Aspire dashboard. Distributed trace context is propagated through Service Bus messages using the W3C `traceparent` header, so a single `POST /matches` request produces a connected trace spanning the API, the queue, the function, and CosmosDB writes.

## Design Notes

- **Vertical slices** — each feature in `MatchesApi` and `LeaderboardsApi` owns its endpoint, handler, and any supporting types in a single folder.
- **CosmosDB schema** — one document per venue in the `leaderboards` container (`leaderboards-db`). Partition key: `/VenueName`. Entries are ordered by `Rank` ascending (Rank 1 = best).
- **CosmosDB client** — the Aspire Azure integration is bypassed in favour of a directly constructed `CosmosClient` (key-based auth, `ConnectionMode.Gateway`, `LimitToEndpoint = true`) to avoid `DefaultAzureCredential` / IMDS issues in local dev. See `Leaderboards.Persistence/PersistenceExtensions.cs`.
