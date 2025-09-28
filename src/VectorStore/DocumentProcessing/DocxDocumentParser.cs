using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using VectorStore.Models;
using VectorStoreDocumentType = VectorStore.Models.DocumentType;

namespace VectorStore.DocumentProcessing;

/// <summary>
/// Parser for Microsoft Word documents (.docx).
/// </summary>
public class DocxDocumentParser : BaseDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".docx" };

    public override VectorStoreDocumentType DocumentType => VectorStoreDocumentType.Docx;

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
        
        // Extract text and metadata from Word document
        var (content, metadata) = await ExtractWordContentAsync(filePath);
        
        // Create chunks using smart chunking
        var chunks = _chunkingService.CreateSmartChunks(content, DocumentType, chunkingOptions);

        // Add chunk-specific metadata
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Metadata["document_type"] = "docx";
            chunks[i].Metadata["chunk_index"] = i;
            chunks[i].Metadata["total_chunks"] = chunks.Count;
            
            // Add section information if available
            if (chunks[i].Metadata.ContainsKey("section_title"))
            {
                chunks[i].Metadata["section_title"] = chunks[i].Metadata["section_title"];
            }
        }

        var additionalMetadata = new Dictionary<string, object>
        {
            ["title"] = metadata.GetValueOrDefault("title", Path.GetFileNameWithoutExtension(filePath)),
            ["author"] = metadata.GetValueOrDefault("author", string.Empty),
            ["subject"] = metadata.GetValueOrDefault("subject", string.Empty),
            ["keywords"] = metadata.GetValueOrDefault("keywords", string.Empty),
            ["description"] = metadata.GetValueOrDefault("description", string.Empty),
            ["created_date"] = metadata.GetValueOrDefault("created_date", string.Empty),
            ["modified_date"] = metadata.GetValueOrDefault("modified_date", string.Empty),
            ["word_count"] = metadata.GetValueOrDefault("word_count", 0),
            ["has_headers"] = metadata.GetValueOrDefault("has_headers", false),
            ["has_tables"] = metadata.GetValueOrDefault("has_tables", false)
        };

        return CreateParseResult(filePath, content, chunks, additionalMetadata);
    }

    private async Task<(string content, Dictionary<string, object> metadata)> ExtractWordContentAsync(string filePath)
    {
        var content = new StringBuilder();
        var metadata = new Dictionary<string, object>();
        var currentSection = string.Empty;
        var hasHeaders = false;
        var hasTables = false;

        using var wordDocument = WordprocessingDocument.Open(filePath, false);
        
        // Extract document properties
        var coreProps = wordDocument.PackageProperties;
        if (coreProps != null)
        {
            metadata["title"] = coreProps.Title ?? string.Empty;
            metadata["author"] = coreProps.Creator ?? string.Empty;
            metadata["subject"] = coreProps.Subject ?? string.Empty;
            metadata["keywords"] = coreProps.Keywords ?? string.Empty;
            metadata["description"] = coreProps.Description ?? string.Empty;
            metadata["created_date"] = coreProps.Created?.ToString() ?? string.Empty;
            metadata["modified_date"] = coreProps.Modified?.ToString() ?? string.Empty;
        }

        // Extract main document content
        var mainPart = wordDocument.MainDocumentPart;
        if (mainPart?.Document?.Body != null)
        {
            var body = mainPart.Document.Body;
            
            foreach (var element in body.Elements())
            {
                switch (element)
                {
                    case Paragraph paragraph:
                        var paragraphText = ExtractParagraphText(paragraph);
                        if (!string.IsNullOrWhiteSpace(paragraphText))
                        {
                            // Check if this is a header
                            if (IsHeader(paragraph))
                            {
                                hasHeaders = true;
                                currentSection = paragraphText.Trim();
                                content.AppendLine($"# {paragraphText.Trim()}");
                            }
                            else
                            {
                                content.AppendLine(paragraphText);
                            }
                        }
                        break;
                        
                    case Table table:
                        hasTables = true;
                        var tableText = ExtractTableText(table);
                        if (!string.IsNullOrWhiteSpace(tableText))
                        {
                            content.AppendLine(tableText);
                        }
                        break;
                }
            }
        }

        metadata["has_headers"] = hasHeaders;
        metadata["has_tables"] = hasTables;
        metadata["word_count"] = content.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        return (content.ToString(), metadata);
    }

    private string ExtractParagraphText(Paragraph paragraph)
    {
        var text = new StringBuilder();
        
        foreach (var run in paragraph.Elements<Run>())
        {
            foreach (var textElement in run.Elements<Text>())
            {
                text.Append(textElement.Text);
            }
        }
        
        return text.ToString().Trim();
    }

    private bool IsHeader(Paragraph paragraph)
    {
        // Check if paragraph has heading style
        var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val;
        if (style != null)
        {
            return style.Value?.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) == true ||
                   style.Value?.StartsWith("Title", StringComparison.OrdinalIgnoreCase) == true;
        }

        // Check if paragraph has heading properties
        var outlineLevel = paragraph.ParagraphProperties?.OutlineLevel?.Val;
        if (outlineLevel != null)
        {
            return outlineLevel.Value <= 6; // H1-H6
        }

        return false;
    }

    private string ExtractTableText(Table table)
    {
        var tableText = new StringBuilder();
        
        foreach (var row in table.Elements<TableRow>())
        {
            var rowText = new StringBuilder();
            
            foreach (var cell in row.Elements<TableCell>())
            {
                var cellText = new StringBuilder();
                
                foreach (var paragraph in cell.Elements<Paragraph>())
                {
                    cellText.Append(ExtractParagraphText(paragraph));
                    cellText.Append(" ");
                }
                
                rowText.Append(cellText.ToString().Trim());
                rowText.Append(" | ");
            }
            
            if (rowText.Length > 0)
            {
                tableText.AppendLine(rowText.ToString().TrimEnd(' ', '|'));
            }
        }
        
        return tableText.ToString();
    }

    protected override SmartChunkingOptions GetDefaultOptions()
    {
        return new SmartChunkingOptions
        {
            MaxChunkSize = 800,
            MinChunkSize = 200,
            OverlapSize = 100,
            Strategy = SmartChunkingStrategy.Hybrid,
            PreserveHeaders = true,
            RespectDocumentStructure = true
        };
    }
}
