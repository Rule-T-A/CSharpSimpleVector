using VectorStore.Models;

namespace VectorStore.DocumentProcessing;

/// <summary>
/// Factory for creating document parsers based on file type.
/// </summary>
public class DocumentParserFactory
{
    private readonly List<IDocumentParser> _parsers;

    public DocumentParserFactory()
    {
        _parsers = new List<IDocumentParser>
        {
            new TextDocumentParser(),
            new MarkdownDocumentParser(),
            new PdfDocumentParser(),
            new DocxDocumentParser()
        };
    }

    /// <summary>
    /// Gets the appropriate parser for the specified file.
    /// </summary>
    public IDocumentParser? GetParser(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        return _parsers.FirstOrDefault(p => p.CanParse(filePath));
    }

    /// <summary>
    /// Gets all available parsers.
    /// </summary>
    public IReadOnlyList<IDocumentParser> GetAllParsers()
    {
        return _parsers.AsReadOnly();
    }

    /// <summary>
    /// Gets the parser for a specific document type.
    /// </summary>
    public IDocumentParser? GetParser(DocumentType documentType)
    {
        return _parsers.FirstOrDefault(p => p.DocumentType == documentType);
    }

    /// <summary>
    /// Determines if a file can be parsed by any available parser.
    /// </summary>
    public bool CanParse(string filePath)
    {
        return GetParser(filePath) != null;
    }

    /// <summary>
    /// Gets the document type of a file.
    /// </summary>
    public DocumentType GetDocumentType(string filePath)
    {
        var parser = GetParser(filePath);
        return parser?.DocumentType ?? DocumentType.Unknown;
    }
}
