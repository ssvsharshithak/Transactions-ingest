using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TransactionsIngest.App.Data;
using TransactionsIngest.App.Models;
using TransactionsIngest.App.Options;
using TransactionsIngest.App.Services;

namespace TransactionsIngest.Tests;

public sealed class IngestionServiceTests
{
    [Fact]
    public async Task RunOnce_InsertsNewTransactions_AndWritesCreatedRevision()
    {
        await using var fixture = await TestFixture.CreateAsync(new FakeClock(new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc)));
        fixture.SnapshotClient.SetData(
        [
            new SnapshotTransaction
            {
                TransactionId = "T-1001",
                CardNumber = "4111111111111111",
                LocationCode = "STO-01",
                ProductName = "Mouse",
                Amount = 19.99m,
                Timestamp = new DateTime(2026, 3, 9, 11, 0, 0, DateTimeKind.Utc)
            }
        ]);

        var result = await fixture.Service.RunOnceAsync();

        Assert.Equal(1, result.Inserted);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Revoked);
        Assert.Equal(0, result.Finalized);

        var stored = await fixture.Db.Transactions.SingleAsync();
        Assert.Equal("1111", stored.CardLast4);
        Assert.False(stored.IsRevoked);
        Assert.False(stored.IsFinalized);

        var revisions = await fixture.Db.TransactionRevisions.ToListAsync();
        Assert.Single(revisions);
        Assert.Equal("Created", revisions[0].ChangeType);
    }

    [Fact]
    public async Task RunOnce_DetectsUpdate_AndIsIdempotentForUnchangedInput()
    {
        await using var fixture = await TestFixture.CreateAsync(new FakeClock(new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc)));
        fixture.SnapshotClient.SetData(
        [
            new SnapshotTransaction
            {
                TransactionId = "T-2001",
                CardNumber = "4111111111111111",
                LocationCode = "STO-03",
                ProductName = "Keyboard",
                Amount = 40.00m,
                Timestamp = new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc)
            }
        ]);

        await fixture.Service.RunOnceAsync();
        var revisionCountAfterFirst = await fixture.Db.TransactionRevisions.CountAsync();

        fixture.SnapshotClient.SetData(
        [
            new SnapshotTransaction
            {
                TransactionId = "T-2001",
                CardNumber = "4111111111111111",
                LocationCode = "STO-03",
                ProductName = "Keyboard",
                Amount = 42.50m,
                Timestamp = new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc)
            }
        ]);

        var updateResult = await fixture.Service.RunOnceAsync();
        var revisionCountAfterSecond = await fixture.Db.TransactionRevisions.CountAsync();

        Assert.Equal(1, updateResult.Updated);
        Assert.True(revisionCountAfterSecond > revisionCountAfterFirst);

        var transaction = await fixture.Db.Transactions.SingleAsync(x => x.TransactionId == "T-2001");
        Assert.Equal(42.50m, transaction.Amount);

        var amountRevisions = await fixture.Db.TransactionRevisions
            .Where(x => x.TransactionId == "T-2001" && x.FieldName == "Amount")
            .ToListAsync();
        Assert.Single(amountRevisions);

        var beforeThirdRunRevisionCount = await fixture.Db.TransactionRevisions.CountAsync();
        var thirdResult = await fixture.Service.RunOnceAsync();
        var afterThirdRunRevisionCount = await fixture.Db.TransactionRevisions.CountAsync();

        Assert.Equal(0, thirdResult.Inserted);
        Assert.Equal(0, thirdResult.Updated);
        Assert.Equal(beforeThirdRunRevisionCount, afterThirdRunRevisionCount);
    }

    [Fact]
    public async Task RunOnce_RevokesMissingRecentTransactions_AndFinalizesOldTransactions()
    {
        var now = new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc);
        await using var fixture = await TestFixture.CreateAsync(new FakeClock(now));

        fixture.Db.Transactions.AddRange(
            new Transaction
            {
                TransactionId = "T-3001",
                CardLast4 = "1111",
                LocationCode = "STO-10",
                ProductName = "Headset",
                Amount = 30.00m,
                TransactionTimeUtc = now.AddHours(-2),
                IsRevoked = false,
                IsFinalized = false,
                FirstSeenAtUtc = now.AddHours(-2),
                LastSeenAtUtc = now.AddHours(-2)
            },
            new Transaction
            {
                TransactionId = "T-3002",
                CardLast4 = "2222",
                LocationCode = "STO-11",
                ProductName = "Monitor",
                Amount = 120.00m,
                TransactionTimeUtc = now.AddHours(-30),
                IsRevoked = false,
                IsFinalized = false,
                FirstSeenAtUtc = now.AddHours(-30),
                LastSeenAtUtc = now.AddHours(-30)
            });
        await fixture.Db.SaveChangesAsync();

        fixture.SnapshotClient.SetData([]);

        var result = await fixture.Service.RunOnceAsync();

        Assert.Equal(1, result.Revoked);
        Assert.Equal(1, result.Finalized);

        var recent = await fixture.Db.Transactions.SingleAsync(x => x.TransactionId == "T-3001");
        var old = await fixture.Db.Transactions.SingleAsync(x => x.TransactionId == "T-3002");
        Assert.True(recent.IsRevoked);
        Assert.True(old.IsFinalized);

        var revokeRevision = await fixture.Db.TransactionRevisions
            .SingleAsync(x => x.TransactionId == "T-3001" && x.ChangeType == "Revoked");
        var finalizeRevision = await fixture.Db.TransactionRevisions
            .SingleAsync(x => x.TransactionId == "T-3002" && x.ChangeType == "Finalized");

        Assert.Equal("IsRevoked", revokeRevision.FieldName);
        Assert.Equal("IsFinalized", finalizeRevision.FieldName);
    }

    [Fact]
    public async Task RunOnce_UnrevokesTransaction_WhenItReappearsInSnapshot()
    {
        var now = new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc);
        await using var fixture = await TestFixture.CreateAsync(new FakeClock(now));

        // Seed a previously-revoked transaction within the 24-hour window.
        fixture.Db.Transactions.Add(new Transaction
        {
            TransactionId = "T-4001",
            CardLast4 = "1111",
            LocationCode = "STO-20",
            ProductName = "Headphones",
            Amount = 59.99m,
            TransactionTimeUtc = now.AddHours(-2),
            IsRevoked = true,
            IsFinalized = false,
            FirstSeenAtUtc = now.AddHours(-3),
            LastSeenAtUtc = now.AddHours(-3)
        });
        await fixture.Db.SaveChangesAsync();

        // Transaction reappears in the new snapshot.
        fixture.SnapshotClient.SetData(
        [
            new SnapshotTransaction
            {
                TransactionId = "T-4001",
                CardNumber = "4111111111111111",
                LocationCode = "STO-20",
                ProductName = "Headphones",
                Amount = 59.99m,
                Timestamp = now.AddHours(-2)
            }
        ]);

        var result = await fixture.Service.RunOnceAsync();

        Assert.Equal(1, result.Updated);

        var stored = await fixture.Db.Transactions.SingleAsync(x => x.TransactionId == "T-4001");
        Assert.False(stored.IsRevoked);

        var unRevokeRevision = await fixture.Db.TransactionRevisions
            .SingleAsync(x => x.TransactionId == "T-4001" && x.FieldName == "IsRevoked");
        Assert.Equal("Updated", unRevokeRevision.ChangeType);
        Assert.Equal("true", unRevokeRevision.OldValue);
        Assert.Equal("false", unRevokeRevision.NewValue);
    }

    [Fact]
    public async Task RunOnce_SkipsFinalizedTransaction_EvenWhenItReappearsInSnapshot()
    {
        var now = new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc);
        await using var fixture = await TestFixture.CreateAsync(new FakeClock(now));

        // Seed a finalized transaction.
        fixture.Db.Transactions.Add(new Transaction
        {
            TransactionId = "T-5001",
            CardLast4 = "2222",
            LocationCode = "STO-30",
            ProductName = "Webcam",
            Amount = 45.00m,
            TransactionTimeUtc = now.AddHours(-30),
            IsRevoked = false,
            IsFinalized = true,
            FirstSeenAtUtc = now.AddHours(-30),
            LastSeenAtUtc = now.AddHours(-30)
        });
        await fixture.Db.SaveChangesAsync();

        // Same transaction reappears with a changed amount.
        fixture.SnapshotClient.SetData(
        [
            new SnapshotTransaction
            {
                TransactionId = "T-5001",
                CardNumber = "4222222222222222",
                LocationCode = "STO-30",
                ProductName = "Webcam",
                Amount = 99.99m,
                Timestamp = now.AddHours(-30)
            }
        ]);

        var result = await fixture.Service.RunOnceAsync();

        Assert.Equal(0, result.Inserted);
        Assert.Equal(0, result.Updated);

        var stored = await fixture.Db.Transactions.SingleAsync(x => x.TransactionId == "T-5001");
        Assert.Equal(45.00m, stored.Amount);
        Assert.True(stored.IsFinalized);

        var revisions = await fixture.Db.TransactionRevisions.ToListAsync();
        Assert.Empty(revisions);
    }

    [Fact]
    public async Task RunOnce_DeduplicatesSnapshot_KeepsEntryWithLatestTimestamp()
    {
        var now = new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc);
        await using var fixture = await TestFixture.CreateAsync(new FakeClock(now));

        // Two entries for the same TransactionId — different amounts and timestamps.
        fixture.SnapshotClient.SetData(
        [
            new SnapshotTransaction
            {
                TransactionId = "T-6001",
                CardNumber = "4333333333333333",
                LocationCode = "STO-40",
                ProductName = "Monitor",
                Amount = 199.99m,
                Timestamp = now.AddHours(-5)   // earlier
            },
            new SnapshotTransaction
            {
                TransactionId = "T-6001",
                CardNumber = "4333333333333333",
                LocationCode = "STO-40",
                ProductName = "Monitor",
                Amount = 249.99m,
                Timestamp = now.AddHours(-1)   // later — this one wins
            }
        ]);

        var result = await fixture.Service.RunOnceAsync();

        Assert.Equal(1, result.Inserted);

        var stored = await fixture.Db.Transactions.SingleAsync(x => x.TransactionId == "T-6001");
        Assert.Equal(249.99m, stored.Amount);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public AppDbContext Db { get; }

        public FakeSnapshotClient SnapshotClient { get; }

        public IngestionService Service { get; }

        private TestFixture(
            SqliteConnection connection,
            AppDbContext db,
            FakeSnapshotClient snapshotClient,
            IngestionService service)
        {
            _connection = connection;
            Db = db;
            SnapshotClient = snapshotClient;
            Service = service;
        }

        public static async Task<TestFixture> CreateAsync(FakeClock clock)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var snapshotClient = new FakeSnapshotClient();
            var service = new IngestionService(
                db,
                snapshotClient,
                clock,
                Options.Create(new IngestionOptions
                {
                    MockFeedPath = "unused.json",
                    EnableFinalization = true
                }),
                NullLogger<IngestionService>.Instance);

            return new TestFixture(connection, db, snapshotClient, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeSnapshotClient : ISnapshotClient
    {
        private IReadOnlyList<SnapshotTransaction> _snapshot = [];

        public void SetData(IReadOnlyList<SnapshotTransaction> snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<IReadOnlyList<SnapshotTransaction>> GetLast24HoursSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_snapshot);
        }
    }

    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
