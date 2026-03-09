namespace TransactionsIngest.App.Services;

public interface ISnapshotClient
{
    Task<IReadOnlyList<SnapshotTransaction>> GetLast24HoursSnapshotAsync(
        CancellationToken cancellationToken = default);
}
