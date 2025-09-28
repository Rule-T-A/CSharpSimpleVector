using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using VectorStore.Models;

namespace VectorStore.DocumentProcessing;

/// <summary>
/// Parser for PDF documents.
/// </summary>
public class PdfDocumentParser : BaseDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".pdf" };

    public override DocumentType DocumentType => DocumentType.Pdf;

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

        var chunkingOptions = options ?? GetDefaultOptions();
        
        // Extract text from PDF
        var (content, metadata) = await ExtractPdfContentAsync(filePath);
        
        // Create chunks using smart chunking
        var chunks = _chunkingService.CreateSmartChunks(content, DocumentType, chunkingOptions);

        // Add chunk-specific metadata
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Metadata["document_type"] = "pdf";
            chunks[i].Metadata["chunk_index"] = i;
            chunks[i].Metadata["total_chunks"] = chunks.Count;
            
            // Add page information if available
            if (chunks[i].Metadata.ContainsKey("page_number"))
            {
                chunks[i].Metadata["page_number"] = chunks[i].Metadata["page_number"];
            }
        }

        var additionalMetadata = new Dictionary<string, object>
        {
            ["title"] = metadata.GetValueOrDefault("title", Path.GetFileNameWithoutExtension(filePath)),
            ["author"] = metadata.GetValueOrDefault("author", string.Empty),
            ["subject"] = metadata.GetValueOrDefault("subject", string.Empty),
            ["creator"] = metadata.GetValueOrDefault("creator", string.Empty),
            ["producer"] = metadata.GetValueOrDefault("producer", string.Empty),
            ["creation_date"] = metadata.GetValueOrDefault("creation_date", string.Empty),
            ["modification_date"] = metadata.GetValueOrDefault("modification_date", string.Empty),
            ["total_pages"] = metadata.GetValueOrDefault("total_pages", 0)
        };

        return CreateParseResult(filePath, content, chunks, additionalMetadata);
    }

    private async Task<(string content, Dictionary<string, object> metadata)> ExtractPdfContentAsync(string filePath)
    {
        var content = new StringBuilder();
        var metadata = new Dictionary<string, object>();
        var pageNumber = 0;

        using var document = PdfDocument.Open(filePath);
        
        // Extract document metadata
        metadata["title"] = document.Information.Title ?? string.Empty;
        metadata["author"] = document.Information.Author ?? string.Empty;
        metadata["subject"] = document.Information.Subject ?? string.Empty;
        metadata["creator"] = document.Information.Creator ?? string.Empty;
        metadata["producer"] = document.Information.Producer ?? string.Empty;
        metadata["creation_date"] = document.Information.CreationDate?.ToString() ?? string.Empty;
        metadata["total_pages"] = document.NumberOfPages;

        foreach (var page in document.GetPages())
        {
            pageNumber++;
            var pageText = ExtractPageText(page);
            
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                // Add page break marker for chunking
                if (content.Length > 0)
                {
                    content.Append('\f'); // Form feed character for page break
                }
                
                content.AppendLine($"--- Page {pageNumber} ---");
                content.AppendLine(pageText);
            }
        }

        return (content.ToString(), metadata);
    }

    private string ExtractPageText(Page page)
    {
        var pageText = new StringBuilder();
        var words = page.GetWords().OrderBy(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();

        if (!words.Any())
            return string.Empty;

        var currentLine = new List<Word>();
        var currentY = words.First().BoundingBox.Bottom;

        foreach (var word in words)
        {
            // Check if this word is on a new line (different Y position)
            if (Math.Abs(word.BoundingBox.Bottom - currentY) > 5) // 5 point tolerance
            {
                // Process the current line
                if (currentLine.Any())
                {
                    pageText.AppendLine(ProcessLine(currentLine));
                    currentLine.Clear();
                }
                currentY = word.BoundingBox.Bottom;
            }

            currentLine.Add(word);
        }

        // Process the last line
        if (currentLine.Any())
        {
            pageText.AppendLine(ProcessLine(currentLine));
        }

        return pageText.ToString().Trim();
    }

    private string ProcessLine(List<Word> words)
    {
        // Sort words by X position (left to right)
        var sortedWords = words.OrderBy(w => w.BoundingBox.Left).ToList();
        
        var lineText = new StringBuilder();
        Word? previousWord = null;

        foreach (var word in sortedWords)
        {
            if (previousWord != null)
            {
                // Calculate space between words
                var space = word.BoundingBox.Left - previousWord.BoundingBox.Right;
                
                // Add space if words are reasonably close together
                if (space > 0 && space < 50) // 50 point threshold
                {
                    lineText.Append(' ');
                }
            }

            lineText.Append(word.Text);
            previousWord = word;
        }

        return lineText.ToString();
    }

    protected override SmartChunkingOptions GetDefaultOptions()
    {
        return new SmartChunkingOptions
        {
            MaxChunkSize = 1000, // Larger chunks for PDFs
            MinChunkSize = 200,
            OverlapSize = 150, // More overlap for PDFs
            Strategy = SmartChunkingStrategy.Hybrid,
            IncludePageNumbers = true
        };
    }
}
