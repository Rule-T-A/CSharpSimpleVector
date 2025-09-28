using System.Text;
using VectorStore.Models;

namespace VectorStore.DocumentProcessing;

/// <summary>
/// Base class for document parsers with common functionality.
/// </summary>
public abstract class BaseDocumentParser : IDocumentParser
{
    protected readonly SmartChunkingService _chunkingService;

    protected BaseDocumentParser()
    {
        _chunkingService = new SmartChunkingService();
    }

    public abstract bool CanParse(string filePath);
    public abstract DocumentType DocumentType { get; }

    public abstract Task<DocumentParseResult> ParseAsync(string filePath, SmartChunkingOptions? options = null);

    /// <summary>
    /// Gets the default chunking options for this document type.
    /// </summary>
    protected virtual SmartChunkingOptions GetDefaultOptions()
    {
        return new SmartChunkingOptions();
    }

    /// <summary>
    /// Extracts basic metadata from a file.
    /// </summary>
    protected Dictionary<string, object> ExtractBasicMetadata(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return new Dictionary<string, object>
        {
            ["file_name"] = fileInfo.Name,
            ["file_path"] = filePath,
            ["file_size"] = fileInfo.Length,
            ["created_date"] = fileInfo.CreationTimeUtc,
            ["modified_date"] = fileInfo.LastWriteTimeUtc,
            ["file_extension"] = fileInfo.Extension.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Creates a document parse result with the specified content and metadata.
    /// </summary>
    protected DocumentParseResult CreateParseResult(string filePath, string content, List<DocumentChunk> chunks, Dictionary<string, object>? additionalMetadata = null)
    {
        var metadata = ExtractBasicMetadata(filePath);
        if (additionalMetadata != null)
        {
            foreach (var kvp in additionalMetadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        return new DocumentParseResult
        {
            FilePath = filePath,
            Title = Path.GetFileNameWithoutExtension(filePath),
            Chunks = chunks,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Safely reads a file with encoding detection.
    /// </summary>
    protected async Task<string> ReadFileWithEncodingAsync(string filePath)
    {
        try
        {
            // Try UTF-8 first
            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            return content;
        }
        catch (DecoderFallbackException)
        {
            // Fallback to default encoding
            var content = await File.ReadAllTextAsync(filePath, Encoding.Default);
            return content;
        }
    }
}
