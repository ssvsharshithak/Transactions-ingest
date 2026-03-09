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

- `IngestionServiceTests.cs` - insert, update/audit, revocation/finalization, idempotency checks.
