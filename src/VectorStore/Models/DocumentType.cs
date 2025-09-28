namespace VectorStore.Models;

/// <summary>
/// Types of documents that can be processed.
/// </summary>
public enum DocumentType
{
    /// <summary>
    /// Plain text files (.txt).
    /// </summary>
    Text,

    /// <summary>
    /// Markdown files (.md, .markdown).
    /// </summary>
    Markdown,

    /// <summary>
    /// PDF files (.pdf).
    /// </summary>
    Pdf,

    /// <summary>
    /// Microsoft Word documents (.docx).
    /// </summary>
    Docx,

    /// <summary>
    /// Unknown or unsupported document type.
    /// </summary>
    Unknown
}
