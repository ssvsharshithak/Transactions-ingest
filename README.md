# Transactions Ingest (Coding Exercise)

Console application for hourly transaction ingestion with EF Core + SQLite.

## What This Implements

- Single-run ingestion flow (designed to be triggered by an external scheduler).
- Mock snapshot loading from a local JSON file.
- Upsert by `TransactionId`: insert if new, detect and record field-level changes if existing.
- Revocation: transactions missing from the current snapshot while still within the 24-hour window are marked as revoked.
- Optional finalization: records older than 24 hours are locked and cannot change afterward.
- Idempotent behavior: repeated runs with unchanged input produce no new revisions.
- Full audit trail: every state change (insert, update, revoke, finalize) is recorded in a `TransactionRevisions` table.
- 6 automated tests covering all key behaviors.

## Project Layout

```
transactions-ingest/
├── src/TransactionsIngest.App/       # Console app and ingestion logic
│   ├── Program.cs                    # DI setup, migration, single run
│   ├── appsettings.json              # Connection string and ingestion config
│   ├── Models/                       # Transaction + TransactionRevision entities
│   ├── Data/                         # EF Core DbContext and migrations
│   ├── Services/                     # IngestionService, MockSnapshotClient, IClock
│   └── Options/                      # Typed config (IngestionOptions)
├── tests/TransactionsIngest.Tests/   # xUnit tests (in-memory SQLite)
├── mocks/transactions.snapshot.json  # Local mock feed (7 transactions)
└── docs/                             # How-to-test guide and project map
```

## Prerequisites

- .NET SDK `10.0.x`

## Build and Run

```bash
dotnet build TransactionsIngest.slnx
dotnet run --project src/TransactionsIngest.App
```

The database (`transactions.db`) is created automatically at the working directory on first run.

## Tests

```bash
dotnet test TransactionsIngest.slnx
```

6 tests covering insert, update, idempotency, revocation, finalization, un-revoke, and deduplication.

## Database

- SQLite, configured in `appsettings.json` under `ConnectionStrings:DefaultConnection`.
- Default: `Data Source=transactions.db` (relative to working directory).
- Migrations are applied automatically on startup via `db.Database.MigrateAsync()`.
- To reset: delete `transactions.db` and run again.

## Configuration

`Ingestion` section in `appsettings.json`:

| Key | Description |
|---|---|
| `MockFeedPath` | Path to the mock JSON feed file |
| `EnableFinalization` | If `true`, records older than 24 hours are finalized and locked |
| `ApiUrl` | Placeholder for future real API integration |

## Approach

Each run executes a single atomic DB transaction:

1. Load and deduplicate the snapshot (latest timestamp wins for duplicate IDs).
2. Fetch all non-finalized records from the DB (plus any finalized ones present in the snapshot).
3. **Upsert** — insert new records; detect and record field-level changes for existing ones.
4. **Revoke** — any record within the 24-hour window that is absent from the snapshot is marked revoked.
5. **Finalize** — if enabled, any record older than 24 hours is marked finalized (immutable thereafter).
6. Write all changes and revisions atomically; commit.

## Assumptions

- `TransactionId` is treated as `string` because the sample data contains IDs like `T-1001`.
- Card numbers are not stored raw; only the last 4 digits (`CardLast4`) are persisted.
- The snapshot is authoritative for the last 24-hour window.
- A transaction that was revoked but reappears in a later snapshot is automatically un-revoked.

