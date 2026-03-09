namespace TransactionsIngest.App.Services;

public readonly record struct IngestionResult(
    int Inserted,
    int Updated,
    int Revoked,
    int Finalized,
    int RevisionsWritten);
