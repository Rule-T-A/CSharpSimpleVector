namespace VectorStore.Models;

/// <summary>
/// Represents a search query with vector, text, and filter criteria.
/// </summary>
public record SearchQuery
{
    public float[]? Vector { get; init; }
    public string? TextQuery { get; init; } // Search in content
    public Dictionary<string, object>? Filters { get; init; }
    public int Limit { get; init; } = 10;
    public float MinSimilarity { get; init; } = 0.0f;
}
