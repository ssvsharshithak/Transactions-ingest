<<<<<<< HEAD
# Transactions Ingest (Coding Exercise)

Console application for hourly transaction ingestion with EF Core + SQLite.

## What This Implements

- Single-run ingestion flow.
- Mock snapshot loading from JSON.
- Upsert by `TransactionId`.
- Field-level change detection with audit records.
- Revocation for missing in-scope records (last 24 hours).
- Optional finalization for records older than 24 hours.
- Idempotent behavior for unchanged repeated runs.
- Automated tests for insert/update/revocation/finalization/idempotency behavior.

## Project Layout

- `src/TransactionsIngest.App` - Console app and ingestion logic.
- `tests/TransactionsIngest.Tests` - Automated tests.
- `mocks/transactions.snapshot.json` - Local mock feed.
- `docs/` - Demo walk-through and quick file map.

## Prerequisites

- .NET SDK `10.0.x`

## Build and Run

```bash
dotnet build TransactionsIngest.slnx
dotnet run --project src/TransactionsIngest.App/TransactionsIngest.App.csproj
```

## Tests

```bash
dotnet test TransactionsIngest.slnx
```

## Database

- SQLite database configured in `src/TransactionsIngest.App/appsettings.json`.
- Default connection string: `Data Source=transactions.db`.
- Migrations are in `src/TransactionsIngest.App/Data/Migrations`.

## Configuration

`Ingestion` section in `appsettings.json`:

- `MockFeedPath` - path to mock JSON feed.
- `EnableFinalization` - if `true`, records older than 24 hours are finalized.
- `ApiUrl` - placeholder for real API integration.

## Assumptions

- `TransactionId` is treated as `string` because the sample contains IDs like `T-1001`.
- Card numbers are not stored raw; only `last4` is stored.
- Snapshot is authoritative for the last 24-hour window.

## Time Tracking

- Estimated hours: `TBD`
- Actual hours: `TBD`
=======
# Transactions-ingest
>>>>>>> 
