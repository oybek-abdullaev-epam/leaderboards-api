---
marp: true
theme: default
paginate: true
size: 16:9
---


# Leaderboards API on Azure


From MVP to scalable architecture


---


## Functional requirements


Build an API for a mobile app:
- Submit match results
- Read leaderboard


Constraints:
- `LeaderboardService` is out-of-scope (Nuget library)
- Leaderboard compute is **$O(n)$** in matches


---


## Non-functional requirements (MVP)


- MVP stage
- ~10 users (1 venue)
- ~10 matches per user


Priorities:
- Correctness (no missing matches)
- Simple operations
- Clear evolution path


---


## Baseline data model

- `POST /matches` (MatchId, WinnerId, LoserId)
- `GET /leaderboards`

**Matches**
- `Id` (PK)
- `WinnerId`
- `LoserId`

**Leaderboard**
- `UserId` (PK)
- `Skill` (1–100)
- `Rank`


---


## Synchronous


1. Insert match
2. Call `LeaderboardService`
3. Write leaderboard updates
4. Commit


Pros:
- Strong guarantees: match + leaderboard consistent
- No lost triggers


Cons:
- Request time grows with matches ($O(n)$)
- If compute fails → match not persisted


---


## Hosting choices (MVP)

- Unlimited Azure credits

**Functions**
- Serverless, scale-to-zero
- Not natural for full API (no middleware, etc.)


**App Service**
- Fully managed, always on
- Simple for HTTP APIs, higher cost


**Container Apps**
- Flexible scaling, but more ops overhead


---


## Database choices


Relational:
- Azure SQL Database (simple, start here, revisit if needed)


Write strategies:
- Row-by-row upsert (simple, more round-trips)
- SQL `MERGE` (fast, careful with concurrency)
- `DELETE` + batch `INSERT` (bulk, needs transaction)


---


## Scale pain (100 users, 2 venues)


Now we have:
- ~100 users, ~1,000 matches


Problems:
- Request time is a bottleneck
- Matches are discarded
- **Eventual consistency is acceptable** (10 seconds)


---


## Fire-and-forget


Flow:
1. Insert match + commit
2. Trigger compute (e.g., `Task.Run`)


Pros:
- Fast `POST /matches`
- Match is persisted even if compute fails


Cons:
- Can’t survive restarts
- No retries / dead-letter
- No backpressure


---


## In-memory queue (Channels)


Pattern:
- `POST /matches` enqueues in-memory work


Pros:
- Backpressure and bounded capacity
- Easy DI + error handling


Cons:
- Triggers lost on restart
- Single-instance only


---


## In-memory cache + timer job


Pattern:
- `IMemoryCache` + timer recompute
- State: `NeedsUpdate` / `InProgress` / `UpToDate`


Pros:
- Debouncing
- On restart, default to `NeedsUpdate`


Cons:
- Limited to one instance


---


## Add venues, spiky traffic (1,000 users)


Now we have many venues:
- Separate leaderboard per venue
- Spiky traffic patterns
- Many concurrent matches


---


## Update data model


Endpoints:
- `POST /matches/{venueId}`
- `GET /leaderboards/{venueId}`


Schema:
- Matches: add `VenueId`
- Leaderboards: `(VenueId, UserId)` composite PK


---

## In-memory cache + timer job


Pattern:
- `IMemoryCache` + timer recompute
- State for each venue: `NeedsUpdate` / `InProgress` / `UpToDate`


Pros:
- Debouncing


Cons:
- Triggers may be lost due to restarts
- Limited to one instance


---


## Two services (API + worker)


- Stateless (REST API, consumers) vs stateful (DB, websockets)


Options:
- API: App Service / Container Apps / Functions
- Worker: Functions / Container Apps / WebJobs


Advantages:
- Independent scaling and autoscaling


Disadvantages:
- More complex communication


---


## HTTP vs gRPC


- HTTP: simplest tooling
- gRPC: fast, strong contracts


Payload choice:
- Don’t send all matches
- Send `VenueId`


Advantages:
- Simple, no extra services


Disadvantages:
- Tight coupling
- No retries / dead-letter, no buffering


---


## Remote queue


Introduce a persistent queue:
- Azure Storage Queues (cheap, simple)
- Azure Service Bus (rich features)
- Event Grid (fan-out events, not a queue)


Advantages:
- Decoupling
- Buffering + rate limiting
- Retries / dead-lettering


Disadvantages:
- Must handle queue unavailability


---


## Queue + outbox pattern


Write in one DB transaction:
- `Matches` insert
- `Outbox` insert

Background publisher sends to queue

- Advantage: no lost triggers after commit
- Disadvantages: extra components and operational overhead


---


## Concurrency problem


- Multiple concurrent updates of the same leaderboard
- Race conditions for relaxed isolation, locks for serializable isolation
- Goal: one active computation per venue.


---


## Distributed cache


- IDistributedCache + Azure Redis
- Consumers control that only one consumer is calculating a leaderboard
- Similar to in-memory cache, one state per venue, venue id as key


---


## Service Bus


- Session-enabled queues, venue id as a session id
- Same venue id - sequentially, different venue id -> parallelly
- Advantages: built-in coordination
- Disadvantages: extra work is done (leaderboard is calculated if no longer needed because it was already queued)


---


## Distributed debouncing


- Service Bus + Redis
- Before sending message to queue, check if already queued, skip if it is
- Pros: reduces redundant work
- Cons: extra cache dependency


---


## Scale (1M users)


Pain points:
- Database becomes bottleneck
- Bulk delete + insert is expensive
- Coupled to a single database


---


## Partitioning


- Partition leaderboards by VenueId
- Advantages: fast delete via partition switch
- Disadvantages: won’t eliminate the write cost


---


## NoSQL


- Introduce document store for leaderboards (CosmosDB)
- Store one leaderboard as one document with VenueId as key
- Advantages: atomic leaderboard updates
- Tradeoffs: limited ad-hoc queries


---


## Evolution path


1) Sync single transaction
2) Async in-memory
3) Split worker + async queue
5) Add outbox for reliability
5) Service Bus sessions
6) Partitioning / NoSQL


---


# Q&A


