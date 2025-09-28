using System.Text.Json.Serialization;

namespace VectorStore.Models;

/// <summary>
/// Configuration options for smart document chunking.
/// </summary>
public class SmartChunkingOptions
{
    /// <summary>
    /// Maximum size of a chunk in characters. Default: 800.
    /// </summary>
    public int MaxChunkSize { get; set; } = 800;

    /// <summary>
    /// Minimum size of a chunk in characters. Default: 200.
    /// </summary>
    public int MinChunkSize { get; set; } = 200;

    /// <summary>
    /// Number of characters to overlap between consecutive chunks. Default: 100.
    /// </summary>
    public int OverlapSize { get; set; } = 100;

    /// <summary>
    /// The chunking strategy to use. Default: Hybrid.
    /// </summary>
    public SmartChunkingStrategy Strategy { get; set; } = SmartChunkingStrategy.Hybrid;

    /// <summary>
    /// Whether to preserve headers in chunks. Default: true.
    /// </summary>
    public bool PreserveHeaders { get; set; } = true;

    /// <summary>
    /// Whether to include page numbers in metadata. Default: true.
    /// </summary>
    public bool IncludePageNumbers { get; set; } = true;

    /// <summary>
    /// Whether to respect document structure when chunking. Default: true.
    /// </summary>
    public bool RespectDocumentStructure { get; set; } = true;

    /// <summary>
    /// Maximum number of sentences per chunk. Default: 8.
    /// </summary>
    public int MaxSentencesPerChunk { get; set; } = 8;

    /// <summary>
    /// Maximum number of paragraphs per chunk. Default: 3.
    /// </summary>
    public int MaxParagraphsPerChunk { get; set; } = 3;

    /// <summary>
    /// Whether to automatically optimize chunking based on document characteristics. Default: true.
    /// </summary>
    public bool AutoOptimize { get; set; } = true;
}

/// <summary>
/// The strategy used for smart chunking.
/// </summary>
public enum SmartChunkingStrategy
{
    /// <summary>
    /// Split by semantic boundaries (paragraphs, sentences).
    /// </summary>
    Semantic,

    /// <summary>
    /// Split by document structure (headers, sections).
    /// </summary>
    Structural,

    /// <summary>
    /// Combine semantic and structural approaches.
    /// </summary>
    Hybrid
}
