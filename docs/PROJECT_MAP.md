# Project Map

## Root

- `TransactionsIngest.slnx` - solution entry.
- `README.md` - setup and assumptions.
- `dotnet-tools.json` - local tool manifest (`dotnet-ef`).
- `mocks/` - local snapshot inputs.
- `docs/` - demo notes.

## App (`src/TransactionsIngest.App`)

- `Program.cs` - startup, DI, migrate, run once.
- `appsettings.json` - connection string + ingestion settings.
- `Services/` - ingestion pipeline and snapshot reader.
- `Data/` - EF Core context, design-time factory, migrations.
- `Models/` - `Transaction` and `TransactionRevision`.
- `Options/` - typed config binding (`IngestionOptions`).

## Tests (`tests/TransactionsIngest.Tests`)

- `IngestionServiceTests.cs` - 6 tests covering:
  - Insert with `Created` revision
  - Field-level update detection + idempotency
  - Revocation of missing recent transactions + finalization of old ones
  - Un-revoke when a previously-revoked transaction reappears
  - Finalized records are immutable even if they reappear in the snapshot
  - Snapshot deduplication (latest timestamp wins for duplicate IDs)

## Mock Feed (`mocks/`)

- `transactions.snapshot.json` - 7 transactions used for local runs:
  - `T-1001` to `T-1009` — recent (within 24h), used to demo insert/update/revoke
  - `T-OLD1` — timestamp 2 days old, demos insert + immediate finalization in one run
