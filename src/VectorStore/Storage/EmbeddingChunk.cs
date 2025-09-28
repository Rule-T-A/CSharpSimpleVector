namespace VectorStore.Storage;

/// <summary>
/// Represents a binary chunk containing vector embeddings.
/// </summary>
public class EmbeddingChunk
{
    public int ChunkId { get; set; }
    public int Count { get; set; }
    public int Dimensions { get; set; }
    public float[] Embeddings { get; set; } = Array.Empty<float>();
    public DateTime CreatedAt { get; set; }
    
    // TODO: Implement binary serialization methods
}
