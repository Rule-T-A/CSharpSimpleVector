using VectorStore.Models;

namespace VectorStore.Storage;

/// <summary>
/// Represents a JSON metadata chunk containing document information.
/// </summary>
public class MetadataChunk
{
    public int ChunkId { get; set; }
    public int Count { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<VectorDocument> Documents { get; set; } = new();
    
    // TODO: Implement JSON serialization methods
}
