using System.Text.Json.Serialization;

namespace VectorStore.Models;

/// <summary>
/// Represents a chunk of a document with metadata about its position and context.
/// </summary>
public class DocumentChunk
{
    /// <summary>
    /// The text content of this chunk.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The zero-based index of this chunk within the document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// The starting character position of this chunk in the original document.
    /// </summary>
    public int StartPosition { get; set; }

    /// <summary>
    /// The ending character position of this chunk in the original document.
    /// </summary>
    public int EndPosition { get; set; }

    /// <summary>
    /// Additional metadata about this chunk (page number, section, etc.).
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// The word count of this chunk.
    /// </summary>
    public int WordCount => Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>
    /// The character count of this chunk.
    /// </summary>
    public int CharacterCount => Content.Length;
}
