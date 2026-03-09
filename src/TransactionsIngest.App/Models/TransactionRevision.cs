using System.ComponentModel.DataAnnotations;

namespace TransactionsIngest.App.Models;

public sealed class TransactionRevision
{
    public long Id { get; set; }

    [MaxLength(50)]
    public string TransactionId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string ChangeType { get; set; } = string.Empty;

    [MaxLength(30)]
    public string FieldName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? OldValue { get; set; }

    [MaxLength(100)]
    public string? NewValue { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    public Transaction? Transaction { get; set; }
}
