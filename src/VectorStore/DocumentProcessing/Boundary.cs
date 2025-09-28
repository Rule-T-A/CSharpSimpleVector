namespace VectorStore.DocumentProcessing;

/// <summary>
/// Represents a boundary point in a document where chunking can occur.
/// </summary>
public class Boundary
{
    /// <summary>
    /// The character position of this boundary in the document.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// The type of boundary this represents.
    /// </summary>
    public BoundaryType Type { get; set; }

    /// <summary>
    /// The priority of this boundary (higher = better stopping point).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Additional context about this boundary.
    /// </summary>
    public string? Context { get; set; }

    public Boundary(int position, BoundaryType type, int priority = 1, string? context = null)
    {
        Position = position;
        Type = type;
        Priority = priority;
        Context = context;
    }
}

/// <summary>
/// Types of boundaries that can be detected in documents.
/// </summary>
public enum BoundaryType
{
    /// <summary>
    /// Document header (title, H1, etc.).
    /// </summary>
    Header,

    /// <summary>
    /// Section header (H2, H3, etc.).
    /// </summary>
    Section,

    /// <summary>
    /// Paragraph break (double line break).
    /// </summary>
    Paragraph,

    /// <summary>
    /// Single line break.
    /// </summary>
    Line,

    /// <summary>
    /// Sentence ending (. ! ?).
    /// </summary>
    Sentence,

    /// <summary>
    /// Word boundary (space).
    /// </summary>
    Word,

    /// <summary>
    /// Page break.
    /// </summary>
    Page,

    /// <summary>
    /// Code block boundary.
    /// </summary>
    CodeBlock,

    /// <summary>
    /// List item boundary.
    /// </summary>
    ListItem,

    /// <summary>
    /// Character boundary (last resort).
    /// </summary>
    Character
}
