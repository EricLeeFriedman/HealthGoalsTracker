# HealthGoalsTracker — Authentication and Cloud Contracts

## Status

This document defines the boundary for Phases 12–14. The versioned Azure Functions API,
validation, in-memory repository, and local live verification are implemented. Production
token validation, Cosmos DB persistence, MAUI authentication, and cloud synchronization
still require implementation and Azure/Entra configuration.

## Authentication Contract

### Client configuration

The MAUI app will receive these non-secret build settings:

| Setting | Purpose |
|---------|---------|
| `EntraClientId` | Public application registration ID |
| `EntraAuthority` | External ID tenant authority |
| `EntraRedirectUri` | `msal{EntraClientId}://auth` |
| `ApiBaseUri` | HTTPS Azure Functions base URI |
| `ApiScope` | Delegated API scope exposed by the backend registration |

No client secret belongs in the MAUI application. MSAL tokens use its platform-protected
cache and are never written to application logs.

### Identity rules

1. The app requests an access token through MSAL interactive sign-in, with silent refresh
   used for later API calls.
2. Azure EasyAuth validates signature, issuer, audience, and lifetime before the request
   reaches the Functions worker. When `WEBSITE_AUTH_ENABLED=true`, the worker reads the
   platform-injected client principal and requires the configured delegated scope.
3. The API derives the stable user ID from the validated token subject. It never trusts a
   `UserId` supplied in JSON or a query string.
4. Local rows remain assigned to `"local"` until the first successful sign-in migration.
5. Signing out removes account tokens but does not delete offline data.

### Authentication outcomes

| Outcome | Client behavior |
|---------|-----------------|
| Token available | Send `Authorization: Bearer <token>` |
| Interaction required | Continue offline and show an explicit sign-in action |
| User cancels | Continue offline without treating cancellation as an application error |
| API returns `401` | Attempt one silent refresh, then require interactive sign-in |
| API returns `403` | Surface authorization failure; do not retry |

The local-only `X-HealthGoals-Test-Subject` header is accepted only when both
`AllowDevelopmentIdentity=true` and `AZURE_FUNCTIONS_ENVIRONMENT=Development`. Production
must leave this disabled. Until EasyAuth is configured with the final Entra authority and
audience, the production authentication boundary is not complete.

## API Contract

All authenticated endpoints are versioned beneath `/api/v1`. Requests and responses use
UTF-8 JSON and ISO-8601 UTC timestamps. Every response returns an `X-Correlation-Id`;
clients send one when available and generate one otherwise.

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/api/v1/health` | Unauthenticated deployment health check |
| `POST` | `/api/v1/sync` | Push local changes and pull changes after a cursor |
| `GET` | `/api/v1/goals` | Retrieve active goals and goal tombstones |
| `GET` | `/api/v1/records?from=yyyy-MM-dd&to=yyyy-MM-dd` | Retrieve daily records with entry snapshots |
| `GET` | `/api/v1/measurements?from=yyyy-MM-dd&to=yyyy-MM-dd` | Retrieve body measurements |

`POST /sync` is the normal client path. The narrower `GET` routes support recovery,
diagnostics, and bounded initial hydration.

### Sync request

```json
{
  "deviceId": "installation-guid",
  "cursor": "opaque-server-cursor-or-null",
  "goals": [],
  "dailyRecords": [],
  "measurements": []
}
```

- Collections contain locally changed rows, including their GUID and `UpdatedAt`.
- A daily record includes its `DailyGoalEntry` snapshots so historical names, icons,
  points, and weekly-only state remain stable.
- Empty collections are valid and perform a pull-only sync.
- Collections must be JSON arrays. Explicit `null` collections, `null` entities, and
  `null` daily-record entry arrays return `400 validation_failed`.
- The server ignores any client-provided user identifier and stamps the authenticated
  subject on every row.

### Sync response

```json
{
  "serverTime": "2026-08-31T17:00:00Z",
  "cursor": "opaque-next-cursor",
  "goals": [],
  "dailyRecords": [],
  "measurements": []
}
```

- The cursor is opaque to the client and advances only after the response is durably
  applied locally.
- Cursors are HMAC-signed, bind the change sequence to the authenticated subject, and
  require a secret `CursorSigningKey` of at least 32 characters. Malformed, modified,
  cross-user, and future cursors return `400 validation_failed`.
- Returned collections contain all server changes after the supplied cursor.
- A submitted entity is also returned with its authoritative stored value, even when the
  submission loses conflict resolution.
- Repeating the same request is idempotent.

### Validation rules

- GUID fields must parse and remain stable across devices.
- Dates use exactly `yyyy-MM-dd`.
- Goal points are whole numbers from 1–99 for daily goals. Weekly-only goals do not
  contribute to daily totals.
- A measurement may contain weight, body-fat percentage, notes, or any combination
  accepted by the local product rules.
- Sync batches allow at most 100 goals, 100 records, and 100 measurements. A record allows
  at most 100 entries.
- Goal names allow 120 characters, emoji values 16 characters, and notes 1,000 characters.
- Weight must be greater than 0 and no more than 1,500 pounds. Body fat must be from
  0–100 percent.
- Read ranges allow at most 366 days.
- Invalid requests return `400` with a stable machine-readable error code and correlation
  ID; validation failures are never partially applied.

## Synchronization Semantics

1. SQLite remains the source of truth for interactive application behavior.
2. Every local write commits before synchronization is attempted.
3. Sync failures never roll back local success and never block use of the app.
4. Pending changes persist locally and retry on launch, foreground, connectivity recovery,
   and explicit refresh with bounded exponential backoff.
5. Upserts are keyed by authenticated user plus entity GUID.
6. Conflicts use last-write-wins by UTC `UpdatedAt`. Equal timestamps compare canonical
   serialized payloads ordinally, making the winner independent of arrival order. The
   authoritative winner is returned to every submitting client.
7. Goals use `IsDeleted` and `DeletedAt` tombstones. Measurement deletion must not ship
   until equivalent tombstone fields and tests are added.
8. Daily scoring is recalculated from entry snapshots after merging; client-provided
   cached totals are not authoritative on the server.
9. Weekly scores are derived values and are never synchronized as stored state.

## Cosmos DB Layout

The free-tier design uses one database with three containers:

| Container | Partition key | Contents |
|-----------|---------------|----------|
| `goals` | `/userId` | Goal definitions and tombstones |
| `dailyRecords` | `/userId` | Daily record plus embedded entry snapshots |
| `measurements` | `/userId` | Body measurements |

Documents include `id`, `userId`, `updatedAt`, and a server-maintained change sequence used
to produce opaque sync cursors. Queries must always include the authenticated partition
key to control RU usage.

## Error and Retry Contract

| Status | Meaning | Retry |
|--------|---------|-------|
| `400` | Invalid request | No; fix or quarantine the local queue item |
| `401` | Missing, expired, or invalid token | Refresh once, then require sign-in |
| `403` | Valid identity lacks scope/access | No |
| `404` | Unknown route or entity | No unless resolving a stale local reference |
| `409` | Explicit conflict requiring refreshed state | Pull, merge, retry once |
| `429` | Cosmos or Functions throttling | Honor `Retry-After` |
| `5xx` | Transient backend failure | Bounded exponential backoff |

Retries use the same entity IDs and correlation ID so operations remain idempotent.

## Privacy and Diagnostics

- Never log access tokens, authorization headers, Google identifiers, user IDs, goal names,
  measurement values, body-fat values, notes, or request/response bodies.
- Log operation names, durations, status codes, retry counts, batch sizes, and correlation
  IDs.
- Exported diagnostics remain user initiated.
- Cloud telemetry must use the same redaction policy as local diagnostics.

## Implementation Gates

- The local Functions contract is covered by unit tests and a real Core Tools HTTP run.
- Production authentication requires the Entra tenant, client ID, authority, API audience,
  delegated scope, and EasyAuth configuration.
- Durable persistence requires an Azure subscription plus Cosmos account/database/container
  configuration; the current in-memory repository intentionally loses data on host restart.
- MAUI authentication and cloud synchronization begin after both production boundaries are
  available. Offline retry tests remain mandatory before cloud sync ships.
