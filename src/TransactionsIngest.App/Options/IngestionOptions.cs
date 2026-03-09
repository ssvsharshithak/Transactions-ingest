using System.ComponentModel.DataAnnotations;

namespace TransactionsIngest.App.Options;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    [Required]
    public string MockFeedPath { get; init; } = "mocks/transactions.snapshot.json";

    public string ApiUrl { get; init; } = "mock://transactions";

    public bool EnableFinalization { get; init; } = true;
}
