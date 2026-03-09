using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionsIngest.App.Options;

namespace TransactionsIngest.App.Services;

public sealed class MockSnapshotClient(
    IOptions<IngestionOptions> options,
    ILogger<MockSnapshotClient> logger) : ISnapshotClient
{
    private readonly IngestionOptions _options = options.Value;
    private readonly ILogger<MockSnapshotClient> _logger = logger;

    public async Task<IReadOnlyList<SnapshotTransaction>> GetLast24HoursSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolveFeedPath(_options.MockFeedPath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"Mock feed file was not found at '{resolvedPath}'.",
                resolvedPath);
        }

        await using var stream = File.OpenRead(resolvedPath);
        var snapshot = await JsonSerializer.DeserializeAsync<List<SnapshotTransaction>>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        var count = snapshot?.Count ?? 0;
        _logger.LogInformation("Loaded {Count} transactions from mock feed: {Path}", count, resolvedPath);
        return snapshot ?? [];
    }

    private static string ResolveFeedPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        var directBaseDirectoryCandidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
        if (File.Exists(directBaseDirectoryCandidate))
        {
            return directBaseDirectoryCandidate;
        }

        var probeRoots = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var root in probeRoots)
        {
            var probe = root;
            for (var i = 0; i < 8; i++)
            {
                var candidate = Path.GetFullPath(Path.Combine(probe, configuredPath));
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                var parent = Directory.GetParent(probe);
                if (parent is null)
                {
                    break;
                }

                probe = parent.FullName;
            }
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
    }
}
