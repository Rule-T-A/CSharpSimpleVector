namespace VectorStore.Models;

/// <summary>
/// Represents storage statistics and performance metrics.
/// </summary>
public record StorageStats
{
    public int TotalDocuments { get; init; }
    public int TotalChunks { get; init; }
    public long TotalSizeBytes { get; init; }
    public int ActiveChunks { get; init; }
    public TimeSpan LastOptimization { get; init; }
    public Dictionary<string, object> AdditionalMetrics { get; init; } = new();
}
