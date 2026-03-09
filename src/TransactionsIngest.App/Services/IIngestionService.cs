namespace TransactionsIngest.App.Services;

public interface IIngestionService
{
    Task<IngestionResult> RunOnceAsync(CancellationToken cancellationToken = default);
}
