# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

**Build the solution:**
```bash
dotnet build src/Leaderboards.slnx
```

**Run with Aspire orchestration (recommended — starts PostgreSQL container + pgAdmin + API):**
```bash
dotnet run --project src/Leaderboards.AppHost
```
Or using the Aspire CLI:
```bash
ASPIRE_ALLOW_UNSECURED_TRANSPORT=true /Users/oabdullaev/.aspire/bin/aspire run --project src/Leaderboards.AppHost
```

**Add an EF Core migration:**
```bash
dotnet ef migrations add <MigrationName> --project src/Leaderboards.MatchesApi --output-dir Data/Migrations
```

**Trust HTTPS dev certificate (first-time setup):**
```bash
dotnet dev-certs https --trust
```

## Architecture

.NET 10 ASP.NET Core solution using **.NET Aspire** for local orchestration. The API uses **vertical slices** architecture with **minimal APIs**.

### Projects

- **`Leaderboards.MatchesApi`** — Main REST API. Uses EF Core + Npgsql (PostgreSQL). Migrations are auto-applied in Development on startup via `db.Database.MigrateAsync()`. The connection string `matchesdb` is injected by Aspire at runtime.
- **`Leaderboards.AppHost`** — Aspire orchestration host. Spins up a PostgreSQL container + pgAdmin, creates the `matchesdb` database, and wires the connection string into `matches-api`. Run this project for local dev.
- **`Leaderboards.ServiceDefaults`** — Shared library. Configures OpenTelemetry (tracing, metrics, logging), HTTP resilience, service discovery, and `/health` + `/alive` endpoints (development only).

### Vertical Slices in MatchesApi

Each feature lives in its own folder under `Matches/` with a static `Map(IEndpointRouteBuilder)` method. All slices are registered in `Matches/MatchesEndpoints.cs` via `app.MapMatchesEndpoints()`.

```
Leaderboards.MatchesApi/
├── Data/
│   ├── Match.cs                  — Entity (Guid Id, string WinnerId, string LoserId)
│   ├── MatchesDbContext.cs       — EF Core DbContext
│   └── Migrations/               — EF Core migrations (auto-applied in dev)
├── Matches/
│   ├── MatchesEndpoints.cs       — Registers all slices
│   ├── Create/
│   │   └── CreateMatchEndpoint.cs  — POST /matches
│   └── GetAll/
│       └── GetMatchesEndpoint.cs   — GET /matches
└── Program.cs
```

### Service Defaults Pattern

Every service calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`. This adds OpenTelemetry (OTLP export when `OTEL_EXPORTER_OTLP_ENDPOINT` is set), `StandardResilienceHandler` on all `HttpClient` instances, and `/health` (readiness) + `/alive` (liveness) endpoints in development.

### Implemented API

- `POST /matches` — Body: `{ "winnerId": string, "loserId": string }` → 201 with created match
- `GET /matches` → 200 array of all matches

### Planned API Surface

- `POST /matches/{venueId}` — Submit match result with venue scoping
- `GET /leaderboards/{venueId}` — Read ranked leaderboard for a venue

### Scaling Architecture

`presentation.md` at the repo root documents the intended evolution:
1. **MVP** — Synchronous single transaction
2. **Async** — In-memory channels or fire-and-forget
3. **Distributed** — Outbox pattern + Azure Service Bus with sessions
4. **High scale** — Partitioning by `VenueId`, eventual CosmosDB

### HTTP Ports (development)

- HTTP: `5374`
- HTTPS: `7335`

Test requests are in `src/Leaderboards.MatchesApi/MatchesApi.http`.