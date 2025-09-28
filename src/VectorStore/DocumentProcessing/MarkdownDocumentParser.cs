using Markdig;
using VectorStore.Models;

namespace VectorStore.DocumentProcessing;

/// <summary>
/// Parser for Markdown documents.
/// </summary>
public class MarkdownDocumentParser : BaseDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".md", ".markdown", ".mdown", ".mkd" };
    private readonly MarkdownPipeline _markdownPipeline;

    public override DocumentType DocumentType => DocumentType.Markdown;

    public MarkdownDocumentParser()
    {
        // Configure Markdig pipeline for text extraction
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

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

        var markdownContent = await ReadFileWithEncodingAsync(filePath);
        var chunkingOptions = options ?? GetDefaultOptions();
        
        // Extract title from first header or filename
        var title = ExtractTitle(markdownContent, filePath);
        
        // Create chunks using smart chunking
        var chunks = _chunkingService.CreateSmartChunks(markdownContent, DocumentType, chunkingOptions);

        // Add chunk-specific metadata
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Metadata["document_type"] = "markdown";
            chunks[i].Metadata["chunk_index"] = i;
            chunks[i].Metadata["total_chunks"] = chunks.Count;
            
            // Extract header context for this chunk
            var headerContext = ExtractHeaderContext(markdownContent, chunks[i].StartPosition);
            if (!string.IsNullOrEmpty(headerContext))
            {
                chunks[i].Metadata["header_context"] = headerContext;
            }
        }

        var additionalMetadata = new Dictionary<string, object>
        {
            ["title"] = title,
            ["has_headers"] = HasHeaders(markdownContent),
            ["has_code_blocks"] = HasCodeBlocks(markdownContent),
            ["has_lists"] = HasLists(markdownContent)
        };

        return CreateParseResult(filePath, markdownContent, chunks, additionalMetadata);
    }

    private string ExtractTitle(string markdownContent, string filePath)
    {
        // Look for first H1 header
        var h1Match = System.Text.RegularExpressions.Regex.Match(markdownContent, @"#\s+(.+)", System.Text.RegularExpressions.RegexOptions.Multiline);
        if (h1Match.Success)
        {
            return h1Match.Groups[1].Value.Trim();
        }

        // Look for first H2 header
        var h2Match = System.Text.RegularExpressions.Regex.Match(markdownContent, @"##\s+(.+)", System.Text.RegularExpressions.RegexOptions.Multiline);
        if (h2Match.Success)
        {
            return h2Match.Groups[1].Value.Trim();
        }

        // Fallback to filename
        return Path.GetFileNameWithoutExtension(filePath);
    }

    private string ExtractHeaderContext(string markdownContent, int position)
    {
        // Find the most recent header before this position
        var headerMatches = System.Text.RegularExpressions.Regex.Matches(markdownContent, @"^(#{1,6})\s+(.+)$", System.Text.RegularExpressions.RegexOptions.Multiline);
        
        string? lastHeader = null;
        foreach (System.Text.RegularExpressions.Match match in headerMatches)
        {
            if (match.Index < position)
            {
                lastHeader = match.Groups[2].Value.Trim();
            }
            else
            {
                break;
            }
        }

        return lastHeader ?? string.Empty;
    }

    private bool HasHeaders(string markdownContent)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(markdownContent, @"^#{1,6}\s+", System.Text.RegularExpressions.RegexOptions.Multiline);
    }

    private bool HasCodeBlocks(string markdownContent)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(markdownContent, @"^```", System.Text.RegularExpressions.RegexOptions.Multiline);
    }

    private bool HasLists(string markdownContent)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(markdownContent, @"^[\s]*[-*+]\s+", System.Text.RegularExpressions.RegexOptions.Multiline) ||
               System.Text.RegularExpressions.Regex.IsMatch(markdownContent, @"^[\s]*\d+\.\s+", System.Text.RegularExpressions.RegexOptions.Multiline);
    }

    protected override SmartChunkingOptions GetDefaultOptions()
    {
        return new SmartChunkingOptions
        {
            MaxChunkSize = 800,
            MinChunkSize = 200,
            OverlapSize = 100,
            Strategy = SmartChunkingStrategy.Hybrid,
            PreserveHeaders = true
        };
    }
}
