using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TransactionsIngest.App.Models;

public sealed class Transaction
{
    [Key]
    [MaxLength(50)]
    public string TransactionId { get; set; } = string.Empty;

    [MaxLength(4)]
    public string CardLast4 { get; set; } = string.Empty;

    [MaxLength(20)]
    public string LocationCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string ProductName { get; set; } = string.Empty;

    [Precision(18, 2)]
    public decimal Amount { get; set; }

    public DateTime TransactionTimeUtc { get; set; }

    public bool IsRevoked { get; set; }

    public bool IsFinalized { get; set; }

    public DateTime FirstSeenAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }

    public DateTime? LastChangedAtUtc { get; set; }

    public List<TransactionRevision> Revisions { get; set; } = [];
}
