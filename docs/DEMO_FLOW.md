# How to Test the App

## 1) Build

```bash
dotnet build TransactionsIngest.slnx
```

Expected: build succeeds for app and tests.

## 2) Run Automated Tests

```bash
dotnet test TransactionsIngest.slnx
```

Expected: all 6 tests pass.

| Test | What it verifies |
|---|---|
| `RunOnce_InsertsNewTransactions_AndWritesCreatedRevision` | New transaction is saved with a `Created` audit row |
| `RunOnce_DetectsUpdate_AndIsIdempotentForUnchangedInput` | Field change writes revision; unchanged third run writes nothing |
| `RunOnce_RevokesMissingRecentTransactions_AndFinalizesOldTransactions` | Missing recent → Revoked; missing old → Finalized |
| `RunOnce_UnrevokesTransaction_WhenItReappearsInSnapshot` | Previously revoked transaction reappears → `IsRevoked` flipped back to false |
| `RunOnce_SkipsFinalizedTransaction_EvenWhenItReappearsInSnapshot` | Finalized record is immutable even if it reappears with changed data |
| `RunOnce_DeduplicatesSnapshot_KeepsEntryWithLatestTimestamp` | Duplicate IDs in snapshot → only the latest timestamp entry wins |

---

## 3) Manual Testing with the Mock Feed

The mock feed is at `mocks/transactions.snapshot.json`.
Edit it between runs to trigger different behaviors.

### Step 1 — First run (Insert)

Run the app as-is:

```bash
dotnet run --project src/TransactionsIngest.App
```

Expected output:
```
inserted=7 updated=0 revoked=0 finalized=1 revisions=8
```

- 6 recent transactions inserted → 6 `Created` revisions
- `T-OLD1` inserted and immediately finalized (timestamp is 2 days old) → 2 revisions

---

### Step 2 — Trigger an Update

Change any `amount` in the JSON (e.g. `T-1001` from `14.99` to `24.99`) and run again:

```bash
dotnet run --project src/TransactionsIngest.App
```

Expected:
```
inserted=0 updated=1 revoked=0 finalized=0
```

An `Updated` revision row is written for the `Amount` field with old and new values.

---

### Step 3 — Trigger a Revoke

Remove any recent transaction entry from the JSON (e.g. delete the `T-1002` block) and run again:

```bash
dotnet run --project src/TransactionsIngest.App
```

Expected:
```
inserted=0 updated=0 revoked=1 finalized=0
```

`T-1002` is marked `IsRevoked=true` and a `Revoked` revision row is written.

---

### Step 4 — Verify Idempotency

Run the app again without changing the JSON:

```bash
dotnet run --project src/TransactionsIngest.App
```

Expected:
```
inserted=0 updated=0 revoked=0 finalized=0 revisions=0
```

No new rows written — the run is fully idempotent.

---

### Step 5 — Reset and Start Over

Delete the database and run again from scratch:

```bash
rm transactions.db
dotnet run --project src/TransactionsIngest.App
```

The database is recreated automatically and all migrations are applied.

---

## 4) Inspect the Database

Open `transactions.db` in VS Code using the **SQLite Viewer** extension, or query it directly:

```sql
-- All transactions and their status
SELECT TransactionId, Amount, IsRevoked, IsFinalized FROM Transactions;

-- Full audit log
SELECT TransactionId, ChangeType, FieldName, OldValue, NewValue, ChangedAtUtc
FROM TransactionRevisions
ORDER BY ChangedAtUtc DESC;
```
