namespace VectorStore.Models;

/// <summary>
/// Represents a document with vector embedding and metadata.
/// </summary>
public record VectorDocument
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public float[] Embedding { get; init; } = Array.Empty<float>();
    public string Content { get; init; } = "";
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
