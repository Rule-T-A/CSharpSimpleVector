using VectorStore.Models;

namespace VectorStore.DocumentProcessing;

/// <summary>
/// Interface for parsing different document types into chunks.
/// </summary>
public interface IDocumentParser
{
    /// <summary>
    /// Determines if this parser can handle the specified file.
    /// </summary>
    bool CanParse(string filePath);

    /// <summary>
    /// Parses a document file into chunks using the specified options.
    /// </summary>
    Task<DocumentParseResult> ParseAsync(string filePath, SmartChunkingOptions? options = null);

    /// <summary>
    /// Gets the document type this parser handles.
    /// </summary>
    DocumentType DocumentType { get; }
}
