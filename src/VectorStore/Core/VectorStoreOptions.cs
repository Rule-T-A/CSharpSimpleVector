using Microsoft.Extensions.Logging;

namespace VectorStore.Core;

/// <summary>
/// Configuration options for the vector store.
/// </summary>
public class VectorStoreOptions
{
    public string StorePath { get; set; } = "./vector-store";
    public int ChunkSize { get; set; } = 1000;        // Documents per chunk
    public int EmbeddingDimensions { get; set; } = 768; // Vector dimensions
    public bool EnableContentIndex { get; set; } = true; // Full-text search
    public bool EnableMetadataIndex { get; set; } = true; // Metadata filters
    public int MaxMemoryChunks { get; set; } = 10;    // LRU cache size
    public bool UseMemoryMapping { get; set; } = true; // Memory-mapped files
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    
    // Embedding configuration
    public bool EnableEmbeddingGeneration { get; set; } = true;
    public int EmbeddingCacheSize { get; set; } = 1000; // Max items in memory cache
    public string EmbeddingModelPath { get; set; } = ""; // Custom model path (empty = auto-download)
}
