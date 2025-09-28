namespace VectorStore.Models;

/// <summary>
/// Represents a search result with document and similarity score.
/// </summary>
public record SearchResult
{
    public VectorDocument Document { get; init; } = null!;
    public float Similarity { get; init; }
    public Dictionary<string, object> DebugInfo { get; init; } = new();
}
