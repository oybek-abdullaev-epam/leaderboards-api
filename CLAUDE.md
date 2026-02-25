# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

**Build the solution:**
```bash
dotnet build src/Leaderboards.slnx
```

**Run with Aspire orchestration (recommended — starts PostgreSQL + pgAdmin + Service Bus emulator + CosmosDB emulator + all services):**
```bash
dotnet run --project src/Leaderboards.AppHost
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

.NET 10 ASP.NET Core solution using **.NET Aspire** for local orchestration. APIs use **vertical slices** with **minimal APIs**.

### Projects

- **`Leaderboards.MatchesApi`** — REST API for match creation. EF Core + Npgsql (PostgreSQL). Migrations are auto-applied in Development. Publishes `MatchCreatedMessage` to the `match-created` Service Bus queue after each `POST /matches`.
- **`Leaderboards.LeaderboardsApi`** — REST API for reading leaderboards. Single endpoint: `GET /leaderboards/{venueName}` → reads from CosmosDB via `LeaderboardRepository`.
- **`Leaderboards.Service`** — Azure Functions isolated worker. Subscribes to `match-created`, fetches matches from `matches-api` via `MatchesApiClient`, computes rankings (`LeaderboardBuilder`), and persists to CosmosDB (`LeaderboardRepository`).
- **`Leaderboards.Persistence`** — Shared class library. Contains `LeaderboardDocument`, `LeaderboardEntry`, `LeaderboardRepository`, and `AddLeaderboardsPersistence()` extension method. Referenced by both `LeaderboardsApi` and `Service`.
- **`Leaderboards.Contracts`** — Shared types: `MatchCreatedMessage { DateTime OccurredAtUtc, string VenueName }` and `MatchResponse { Guid Id, string WinnerId, string LoserId, string VenueName }`.
- **`Leaderboards.AppHost`** — Aspire orchestration host. Run this for local dev.
- **`Leaderboards.ServiceDefaults`** — Shared library. Configures OpenTelemetry, `StandardResilienceHandler` on all `HttpClient` instances, and `/health` + `/alive` endpoints in development. Uses C# 14 `extension` member syntax.

### Vertical Slices Pattern

Both `MatchesApi` and `LeaderboardsApi` use this pattern. Each feature lives in its own folder with a static `Map(IEndpointRouteBuilder)` method, registered in a central `*Endpoints.cs` file.

### Current API Surface

**MatchesApi** (HTTP `5374` / HTTPS `7335`):
- `POST /matches` — `{ "winnerId": string, "loserId": string, "venueName": string }` → 201
- `GET /matches?venueName={venueName}` — `venueName` optional → 200 array of `MatchResponse`

**LeaderboardsApi** (HTTP `5375` / HTTPS `7336`):
- `GET /leaderboards/{venueName}` → 200 with ranked leaderboard, 404 if not yet generated

Test requests: `src/Leaderboards.MatchesApi/MatchesApi.http` and `src/Leaderboards.LeaderboardsApi/LeaderboardsApi.http`. OpenAPI at `/openapi/v1.json` (development only).

### CosmosDB Setup — Leaderboards.Persistence

**Do not use `Aspire.Microsoft.Azure.Cosmos` or `AddAzureCosmosClient`** in consuming projects. The Aspire wrapper registers a second `CosmosClient` using `DefaultAzureCredential`, which fails in local dev (IMDS unreachable). Instead, `AddLeaderboardsPersistence()` constructs `CosmosClient` directly:

```csharp
// Both consuming projects call this single method:
builder.AddLeaderboardsPersistence();
```

`PersistenceExtensions.CreateCosmosClient` parses the connection string explicitly and uses `CosmosClient(endpoint, accountKey, options)` — this constructor has no `DefaultAzureCredential` fallback. Key options:
- `ConnectionMode = ConnectionMode.Gateway` — required for the emulator (no Direct mode support)
- `LimitToEndpoint = true` — required: the emulator's discovery response returns its internal Docker address (`127.0.0.1:8081`) as `writableLocations`, which the SDK would follow and fail to reach. This flag forces all requests through the provided endpoint.

**`Newtonsoft.Json` runtime requirement:** `Microsoft.Azure.Cosmos 3.54.0` requires `Newtonsoft.Json` at runtime even when using `System.Text.Json` serialization. It must be explicitly referenced in `Leaderboards.Persistence.csproj`.

**Connection string injection:** Aspire injects with `AccountEndpoint=tcp://localhost:PORT`. The vnext-preview emulator serves plain HTTP, so `PersistenceExtensions` rewrites `tcp://` → `http://` before constructing the client. Aspire uses different config paths per project type:
- Functions: `Aspire:Microsoft:Azure:Cosmos:cosmos:ConnectionString`
- Standard ASP.NET Core: `ConnectionStrings:cosmos`

Both paths are checked with `??` fallback.

**CosmosDB schema:** One document per venue in the `leaderboards` container of `leaderboards-db`. Partition key `/VenueName`; document `id` equals `VenueName`. `Entries` ordered by `Rank` ascending (Rank 1 = best).

**`EnsureInitializedAsync`** uses `CreateDatabaseIfNotExistsAsync` + `CreateContainerIfNotExistsAsync` and is called on every repository operation.

### CosmosDB Emulator Quirks (AppHost)

- ARM64 image: `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview` — the default `latest` tag has no ARM64 manifest.
- Do **not** call `cosmos.AddCosmosDatabase(...)` or `.WaitFor(cosmos)` — both trigger Aspire's SDK health check using the `tcp://` scheme, leaving the resource permanently Unhealthy.
- `cosmos_check` in the Aspire dashboard always shows **Unhealthy** — known limitation. Operations work correctly.

### Azure Functions + Aspire Integration

`Leaderboards.Service` is registered with `AddAzureFunctionsProject` (not `AddProject`). Key behaviors:

- `WithHostStorage(storage)` is required — the Functions host needs Azure Storage for internal coordination.
- **Do not call `builder.AddServiceDefaults()` in the Functions `Program.cs`** — creates a second OTEL pipeline; every log appears twice in the Aspire dashboard.
- Connection string remapping is required: Aspire injects `ConnectionStrings:service-bus`, but the trigger binding expects `service-bus__connectionString`.
- **`AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true)` must be called before `FunctionsApplication.CreateBuilder`** — enables the Azure SDK activity source for distributed trace propagation through Service Bus messages.
- `Leaderboards.Service` configures **tracing only** (not logging) — logs flow through the `func` host process.

### Distributed Trace Propagation

`POST /matches` embeds the W3C trace context as `Diagnostic-Id` on the Service Bus message. `MatchCreatedFunction` restores it manually using `ActivityContext.TryParse` and `ActivitySource.StartActivity(..., parentContext)`. Bind the trigger to `ServiceBusReceivedMessage` directly (not the deserialized type) — the isolated worker runtime does not support binding both simultaneously.

### Service Defaults Pattern

`MatchesApi` and `LeaderboardsApi` call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`. `Leaderboards.Service` does **not**.

### Tests

No test projects. Build success and EF migration application (auto-run in dev) are the main verification steps.

### Scaling Architecture

`presentation.md` at the repo root documents the intended evolution:
1. **MVP** — Synchronous single transaction
2. **Async** — In-memory channels or fire-and-forget
3. **Distributed** — Outbox pattern + Azure Service Bus with sessions
4. **High scale** — Partitioning by `VenueId`, eventual CosmosDB
