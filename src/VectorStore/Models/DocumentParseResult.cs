using System.Text.Json.Serialization;

namespace VectorStore.Models;

/// <summary>
/// Represents the result of parsing a document into chunks.
/// </summary>
public class DocumentParseResult
{
    /// <summary>
    /// The file path of the parsed document.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The title of the document (extracted from metadata or filename).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The list of chunks created from the document.
    /// </summary>
    public List<DocumentChunk> Chunks { get; set; } = new();

    /// <summary>
    /// Metadata about the entire document.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// The total number of chunks created.
    /// </summary>
    public int TotalChunks => Chunks.Count;

    /// <summary>
    /// The total word count across all chunks.
    /// </summary>
    public int TotalWordCount => Chunks.Sum(c => c.WordCount);

    /// <summary>
    /// The total character count across all chunks.
    /// </summary>
    public int TotalCharacterCount => Chunks.Sum(c => c.CharacterCount);
}
