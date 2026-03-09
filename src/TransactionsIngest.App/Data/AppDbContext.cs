using Microsoft.EntityFrameworkCore;
using TransactionsIngest.App.Models;

namespace TransactionsIngest.App.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<TransactionRevision> TransactionRevisions => Set<TransactionRevision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(x => x.TransactionId);
            entity.Property(x => x.TransactionId).HasMaxLength(50);
            entity.Property(x => x.CardLast4).HasMaxLength(4).IsRequired();
            entity.Property(x => x.LocationCode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ProductName).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.TransactionTimeUtc).IsRequired();
            entity.Property(x => x.FirstSeenAtUtc).IsRequired();
            entity.Property(x => x.LastSeenAtUtc).IsRequired();

            entity.HasMany(x => x.Revisions)
                .WithOne(x => x.Transaction)
                .HasForeignKey(x => x.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.TransactionTimeUtc);
            entity.HasIndex(x => x.IsRevoked);
            entity.HasIndex(x => x.IsFinalized);
        });

        modelBuilder.Entity<TransactionRevision>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChangeType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.FieldName).HasMaxLength(30).IsRequired();
            entity.Property(x => x.OldValue).HasMaxLength(100);
            entity.Property(x => x.NewValue).HasMaxLength(100);
            entity.Property(x => x.ChangedAtUtc).IsRequired();

            entity.HasIndex(x => x.TransactionId);
            entity.HasIndex(x => x.ChangedAtUtc);
        });
    }
}
