using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionsIngest.App.Data;
using TransactionsIngest.App.Models;
using TransactionsIngest.App.Options;

namespace TransactionsIngest.App.Services;

public sealed class IngestionService(
    AppDbContext db,
    ISnapshotClient snapshotClient,
    IClock clock,
    IOptions<IngestionOptions> options,
    ILogger<IngestionService> logger) : IIngestionService
{
    private static readonly StringComparer IdComparer = StringComparer.OrdinalIgnoreCase;
    private readonly AppDbContext _db = db;
    private readonly ISnapshotClient _snapshotClient = snapshotClient;
    private readonly IClock _clock = clock;
    private readonly IngestionOptions _options = options.Value;
    private readonly ILogger<IngestionService> _logger = logger;

    public async Task<IngestionResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;
        var cutoffUtc = nowUtc.AddHours(-24);

        var snapshot = await _snapshotClient.GetLast24HoursSnapshotAsync(cancellationToken);
        var incomingTransactions = snapshot
            .Where(static x => !string.IsNullOrWhiteSpace(x.TransactionId))
            .Select(Normalize)
            .GroupBy(x => x.TransactionId, IdComparer)
            .Select(g => g.OrderByDescending(x => x.Timestamp).First())
            .ToList();

        var snapshotIds = incomingTransactions
            .Select(x => x.TransactionId)
            .ToHashSet(IdComparer);

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var trackedTransactions = await _db.Transactions
            .Where(t => !t.IsFinalized || snapshotIds.Contains(t.TransactionId))
            .ToListAsync(cancellationToken);

        var existingById = trackedTransactions.ToDictionary(x => x.TransactionId, IdComparer);

        var inserted = 0;
        var updated = 0;
        var revoked = 0;
        var finalized = 0;
        var revisionsWritten = 0;

        foreach (var incoming in incomingTransactions)
        {
            if (!existingById.TryGetValue(incoming.TransactionId, out var existing))
            {
                var entity = new Transaction
                {
                    TransactionId = incoming.TransactionId,
                    CardLast4 = ToLast4(incoming.CardNumber),
                    LocationCode = Clamp(incoming.LocationCode, 20),
                    ProductName = Clamp(incoming.ProductName, 20),
                    Amount = incoming.Amount,
                    TransactionTimeUtc = incoming.Timestamp,
                    IsRevoked = false,
                    IsFinalized = false,
                    FirstSeenAtUtc = nowUtc,
                    LastSeenAtUtc = nowUtc
                };

                _db.Transactions.Add(entity);
                existingById[entity.TransactionId] = entity;
                revisionsWritten += AddRevision(
                    entity.TransactionId,
                    changeType: "Created",
                    fieldName: "Transaction",
                    oldValue: null,
                    newValue: "Inserted",
                    changedAtUtc: nowUtc);
                inserted++;
                continue;
            }

            if (existing.IsFinalized)
            {
                continue;
            }

            var changed = false;
            var incomingLast4 = ToLast4(incoming.CardNumber);
            var incomingLocation = Clamp(incoming.LocationCode, 20);
            var incomingProduct = Clamp(incoming.ProductName, 20);

            if (!string.Equals(existing.CardLast4, incomingLast4, StringComparison.Ordinal))
            {
                revisionsWritten += AddRevision(
                    existing.TransactionId,
                    changeType: "Updated",
                    fieldName: "CardLast4",
                    oldValue: existing.CardLast4,
                    newValue: incomingLast4,
                    changedAtUtc: nowUtc);
                existing.CardLast4 = incomingLast4;
                changed = true;
            }

            if (!string.Equals(existing.LocationCode, incomingLocation, StringComparison.Ordinal))
            {
                revisionsWritten += AddRevision(
                    existing.TransactionId,
                    changeType: "Updated",
                    fieldName: "LocationCode",
                    oldValue: existing.LocationCode,
                    newValue: incomingLocation,
                    changedAtUtc: nowUtc);
                existing.LocationCode = incomingLocation;
                changed = true;
            }

            if (!string.Equals(existing.ProductName, incomingProduct, StringComparison.Ordinal))
            {
                revisionsWritten += AddRevision(
                    existing.TransactionId,
                    changeType: "Updated",
                    fieldName: "ProductName",
                    oldValue: existing.ProductName,
                    newValue: incomingProduct,
                    changedAtUtc: nowUtc);
                existing.ProductName = incomingProduct;
                changed = true;
            }

            if (existing.Amount != incoming.Amount)
            {
                revisionsWritten += AddRevision(
                    existing.TransactionId,
                    changeType: "Updated",
                    fieldName: "Amount",
                    oldValue: existing.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                    newValue: incoming.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                    changedAtUtc: nowUtc);
                existing.Amount = incoming.Amount;
                changed = true;
            }

            if (existing.TransactionTimeUtc != incoming.Timestamp)
            {
                revisionsWritten += AddRevision(
                    existing.TransactionId,
                    changeType: "Updated",
                    fieldName: "TransactionTimeUtc",
                    oldValue: existing.TransactionTimeUtc.ToString("O", CultureInfo.InvariantCulture),
                    newValue: incoming.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                    changedAtUtc: nowUtc);
                existing.TransactionTimeUtc = incoming.Timestamp;
                changed = true;
            }

            if (existing.IsRevoked)
            {
                revisionsWritten += AddRevision(
                    existing.TransactionId,
                    changeType: "Updated",
                    fieldName: "IsRevoked",
                    oldValue: "true",
                    newValue: "false",
                    changedAtUtc: nowUtc);
                existing.IsRevoked = false;
                changed = true;
            }

            existing.LastSeenAtUtc = nowUtc;

            if (changed)
            {
                existing.LastChangedAtUtc = nowUtc;
                updated++;
            }
        }

        foreach (var existing in existingById.Values)
        {
            if (existing.IsFinalized || snapshotIds.Contains(existing.TransactionId))
            {
                continue;
            }

            if (!existing.IsRevoked && existing.TransactionTimeUtc >= cutoffUtc)
            {
                existing.IsRevoked = true;
                existing.LastChangedAtUtc = nowUtc;
                revisionsWritten += AddRevision(
                    existing.TransactionId,
                    changeType: "Revoked",
                    fieldName: "IsRevoked",
                    oldValue: "false",
                    newValue: "true",
                    changedAtUtc: nowUtc);
                revoked++;
            }
        }

        if (_options.EnableFinalization)
        {
            foreach (var existing in existingById.Values)
            {
                if (existing.IsFinalized || existing.TransactionTimeUtc >= cutoffUtc)
                {
                    continue;
                }

                existing.IsFinalized = true;
                existing.LastChangedAtUtc = nowUtc;
                revisionsWritten += AddRevision(
                    existing.TransactionId,
                    changeType: "Finalized",
                    fieldName: "IsFinalized",
                    oldValue: "false",
                    newValue: "true",
                    changedAtUtc: nowUtc);
                finalized++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Processed snapshotCount={SnapshotCount}, inserted={Inserted}, updated={Updated}, revoked={Revoked}, finalized={Finalized}",
            incomingTransactions.Count,
            inserted,
            updated,
            revoked,
            finalized);

        return new IngestionResult(inserted, updated, revoked, finalized, revisionsWritten);
    }

    private int AddRevision(
        string transactionId,
        string changeType,
        string fieldName,
        string? oldValue,
        string? newValue,
        DateTime changedAtUtc)
    {
        _db.TransactionRevisions.Add(new TransactionRevision
        {
            TransactionId = transactionId,
            ChangeType = changeType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAtUtc = changedAtUtc
        });

        return 1;
    }

    private static SnapshotTransaction Normalize(SnapshotTransaction transaction)
    {
        return transaction with
        {
            TransactionId = transaction.TransactionId.Trim(),
            CardNumber = (transaction.CardNumber ?? string.Empty).Trim(),
            LocationCode = (transaction.LocationCode ?? string.Empty).Trim(),
            ProductName = (transaction.ProductName ?? string.Empty).Trim(),
            Timestamp = EnsureUtc(transaction.Timestamp)
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string Clamp(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private static string ToLast4(string cardNumber)
    {
        var digits = new string((cardNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return "0000";
        }

        return digits.Length >= 4
            ? digits[^4..]
            : digits.PadLeft(4, '0');
    }
}
