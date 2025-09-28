using VectorStore.Models;

namespace VectorStore.DocumentProcessing;

/// <summary>
/// Parser for plain text documents.
/// </summary>
public class TextDocumentParser : BaseDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".txt", ".text", ".log", ".csv" };

    public override DocumentType DocumentType => DocumentType.Text;

    public override bool CanParse(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public override async Task<DocumentParseResult> ParseAsync(string filePath, SmartChunkingOptions? options = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var content = await ReadFileWithEncodingAsync(filePath);
        var chunkingOptions = options ?? GetDefaultOptions();
        
        // Create chunks using smart chunking
        var chunks = _chunkingService.CreateSmartChunks(content, DocumentType, chunkingOptions);

        // Add chunk-specific metadata
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Metadata["document_type"] = "text";
            chunks[i].Metadata["chunk_index"] = i;
            chunks[i].Metadata["total_chunks"] = chunks.Count;
        }

        return CreateParseResult(filePath, content, chunks);
    }

    protected override SmartChunkingOptions GetDefaultOptions()
    {
        return new SmartChunkingOptions
        {
            MaxChunkSize = 300, // Smaller for testing
            MinChunkSize = 100,
            OverlapSize = 50,
            Strategy = SmartChunkingStrategy.Semantic
        };
    }
}
