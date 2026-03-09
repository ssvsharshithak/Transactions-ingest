# Demo Flow

Use this sequence during the demo.

## 1) Build

```bash
dotnet build TransactionsIngest.slnx
```

Expected: build succeeds for app and tests.

## 2) Run One Ingestion Cycle

```bash
dotnet run --project src/TransactionsIngest.App/TransactionsIngest.App.csproj
```

Expected logs:

- mock snapshot loaded
- inserted/updated/revoked/finalized counts
- run complete summary

## 3) Run Tests

```bash
dotnet test TransactionsIngest.slnx
```

Expected: all tests pass.

## 4) Files To Open In Demo

- `src/TransactionsIngest.App/Program.cs`
- `src/TransactionsIngest.App/Services/IngestionService.cs`
- `src/TransactionsIngest.App/Data/AppDbContext.cs`
- `src/TransactionsIngest.App/Models/Transaction.cs`
- `src/TransactionsIngest.App/Models/TransactionRevision.cs`
- `tests/TransactionsIngest.Tests/IngestionServiceTests.cs`
