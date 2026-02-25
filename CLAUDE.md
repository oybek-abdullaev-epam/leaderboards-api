# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

**Build the solution:**
```bash
dotnet build src/Leaderboards.slnx
```

**Run with Aspire orchestration (recommended — starts PostgreSQL + pgAdmin + Service Bus emulator + API + Functions):**
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

.NET 10 ASP.NET Core solution using **.NET Aspire** for local orchestration. The API uses **vertical slices** architecture with **minimal APIs**.

### Projects

- **`Leaderboards.MatchesApi`** — Main REST API. Uses EF Core + Npgsql (PostgreSQL). Migrations are auto-applied in Development on startup via `db.Database.MigrateAsync()`. Connection strings `matches-db` (Postgres) and `service-bus` (Service Bus) are injected by Aspire at runtime.
- **`Leaderboards.Service`** — Azure Functions isolated worker. Subscribes to the `match-created` Service Bus queue, fetches matches from `matches-api` via `MatchesApiClient`, and produces a leaderboard.
- **`Leaderboards.Contracts`** — Shared types referenced by both MatchesApi and Service: `MatchCreatedMessage { DateTime OccurredAtUtc, string VenueName }` and `MatchResponse { Guid Id, string WinnerId, string LoserId, string VenueName }`.
- **`Leaderboards.AppHost`** — Aspire orchestration host (`AppHost.cs`). Spins up PostgreSQL + pgAdmin + Azure Service Bus emulator, and wires all services together. Run this project for local dev.
- **`Leaderboards.ServiceDefaults`** — Shared library. Configures OpenTelemetry (OTLP export when `OTEL_EXPORTER_OTLP_ENDPOINT` is set), `StandardResilienceHandler` on all `HttpClient` instances, and `/health` + `/alive` endpoints in development.

### Vertical Slices in MatchesApi

Each feature lives in its own folder under `Matches/` with a static `Map(IEndpointRouteBuilder)` method. To add a new slice:
1. Create `Matches/<Feature>/<FeatureName>Endpoint.cs` with a `public static void Map(IEndpointRouteBuilder app)` method.
2. Call `FeatureNameEndpoint.Map(app)` inside `MatchesEndpoints.MapMatchesEndpoints()`.

```
Leaderboards.MatchesApi/
├── Data/
│   ├── Match.cs                  — Entity (Guid Id, string WinnerId, string LoserId, string VenueName)
│   ├── MatchesDbContext.cs       — EF Core DbContext
│   └── Migrations/               — EF Core migrations (auto-applied in dev)
├── Matches/
│   ├── MatchesEndpoints.cs       — Registers all slices
│   ├── Create/
│   │   └── CreateMatchEndpoint.cs   — POST /matches; publishes MatchCreatedMessage after save
│   └── GetAll/
│       └── GetMatchesEndpoint.cs    — GET /matches; returns List<MatchResponse>
└── Program.cs
```

### Data Model Notes

`WinnerId` and `LoserId` on the `Match` entity are opaque `string` fields (max 256 chars) — player identity is managed externally. All three string fields have `IsRequired()` and `HasMaxLength(256)` constraints set in `MatchesDbContext.OnModelCreating`.

### Service Bus

`POST /matches` publishes a `MatchCreatedMessage` (JSON) to the `match-created` queue after persisting to the database. `MatchCreatedMessage` intentionally omits winner/loser IDs — it only carries `OccurredAtUtc` and `VenueName`. The `ServiceBusSender` for this queue is registered as a singleton in `Program.cs` and injected into the endpoint handler.

AppHost wires the emulator via `builder.AddAzureServiceBus("service-bus").RunAsEmulator().AddServiceBusQueue("match-created")`. The API publishes with `builder.AddAzureServiceBusClient("service-bus")`.

### Azure Functions + Aspire Integration

`Leaderboards.Service` is registered in AppHost with `AddAzureFunctionsProject` (not `AddProject`). Key behaviors:

- `WithReference(serviceBus)` uses a Functions-specific overload that sets `service-bus` as a full connection string env var.
- `WithReference(matchesApi)` injects the `matches-api` HTTPS endpoint for service discovery.
- `WithHostStorage(storage)` is required — the Functions host needs Azure Storage for internal coordination.
- Aspire auto-sets `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`.
- **Do not call `builder.AddServiceDefaults()` in the Functions `Program.cs`** — the worker inherits `OTEL_EXPORTER_OTLP_ENDPOINT` from the `func` host process, so calling `AddServiceDefaults()` creates a second OTEL pipeline and every log appears twice in the Aspire dashboard.
- The connection string remapping in `Program.cs` is required: Aspire injects `ConnectionStrings:service-bus`, but the Functions trigger binding expects `service-bus__connectionString`.
- **`AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true)` must be called before `FunctionsApplication.CreateBuilder`** — this enables the Azure SDK activity source for proper distributed trace propagation through Service Bus messages. Both MatchesApi and Leaderboards.Service set this switch.

### OpenTelemetry in Leaderboards.Service

`Leaderboards.Service` configures **tracing only** (not logging) via `AddOpenTelemetry().WithTracing(...)`. Logging is intentionally excluded because it flows through the `func` host process to avoid duplicates. The tracing setup includes:
- `AddSource("Leaderboards.Service")` — custom activity source
- `AddHttpClientInstrumentation()` — auto-traces outbound HTTP calls
- `AddOtlpExporter()` — exports to the Aspire dashboard via the inherited `OTEL_EXPORTER_OTLP_ENDPOINT`

### Distributed Trace Propagation

When `POST /matches` publishes to Service Bus, the Azure SDK embeds the W3C trace context as a `Diagnostic-Id` property on the message. `MatchCreatedFunction` restores this context manually:

```csharp
// Bind trigger directly to ServiceBusReceivedMessage — NOT to the deserialized type.
// The isolated worker runtime does NOT support binding both simultaneously.
public async Task Run(
    [ServiceBusTrigger("match-created", Connection = "service-bus")] ServiceBusReceivedMessage message)
{
    var matchCreated = message.Body.ToObjectFromJson<MatchCreatedMessage>()!;
    var diagnosticId = message.ApplicationProperties.GetValueOrDefault("Diagnostic-Id")?.ToString();
    ActivityContext parentContext = default;
    if (diagnosticId is not null)
        ActivityContext.TryParse(diagnosticId, traceState: null, isRemote: true, out parentContext);
    using var activity = ActivitySource.StartActivity("match-created process", ActivityKind.Consumer, parentContext);
    // ...
}
```

The `HttpClient` call to `matches-api` and all logging within the `using var activity` scope appear as children of the original `POST /matches` trace in the Aspire dashboard.

### Service Defaults Pattern

`MatchesApi` calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`. This adds OpenTelemetry, HTTP resilience, and `/health` + `/alive` endpoints in development. `Leaderboards.Service` does **not** call `AddServiceDefaults()` (see above).

`ServiceDefaults/Extensions.cs` uses the **C# 14 `extension` member syntax** (`extension<TBuilder>(TBuilder builder) { ... }`) — this is different from classic `static` extension methods.

### Tests

There are no test projects. Build success and EF migration application (auto-run in dev) are the main verification steps.

### Current API

- `POST /matches` — Body: `{ "winnerId": string, "loserId": string, "venueName": string }` → 201 with created match
- `GET /matches?venueName={venueName}` — `venueName` is optional; omit to return all matches → 200 array of `MatchResponse`

OpenAPI docs available in development at `/openapi/v1.json`. Test requests are in `src/Leaderboards.MatchesApi/MatchesApi.http`.

### HTTP Ports (development)

- HTTP: `5374`
- HTTPS: `7335`

### Planned API Surface

- `GET /leaderboards/{venueId}` — Read ranked leaderboard for a venue

### Scaling Architecture

`presentation.md` at the repo root documents the intended evolution:
1. **MVP** — Synchronous single transaction
2. **Async** — In-memory channels or fire-and-forget
3. **Distributed** — Outbox pattern + Azure Service Bus with sessions
4. **High scale** — Partitioning by `VenueId`, eventual CosmosDB
