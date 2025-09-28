using System.Text.RegularExpressions;
using VectorStore.Models;

namespace VectorStore.DocumentProcessing;

/// <summary>
/// Detects natural boundaries in documents for intelligent chunking.
/// </summary>
public class BoundaryDetector
{
    /// <summary>
    /// Detects boundaries in content based on document type.
    /// </summary>
    public List<Boundary> DetectBoundaries(string content, DocumentType type)
    {
        return type switch
        {
            DocumentType.Markdown => DetectMarkdownBoundaries(content),
            DocumentType.Pdf => DetectPdfBoundaries(content),
            DocumentType.Docx => DetectDocxBoundaries(content),
            DocumentType.Text => DetectTextBoundaries(content),
            _ => DetectTextBoundaries(content) // Fallback to text
        };
    }

    private List<Boundary> DetectMarkdownBoundaries(string content)
    {
        var boundaries = new List<Boundary>();

        // Headers (# ## ###) - highest priority
        var headerMatches = Regex.Matches(content, @"^#{1,6}\s+(.+)$", RegexOptions.Multiline);
        foreach (Match match in headerMatches)
        {
            var level = match.Value.TakeWhile(c => c == '#').Count();
            var priority = 10 - level; // H1 = 9, H2 = 8, etc.
            boundaries.Add(new Boundary(match.Index, BoundaryType.Header, priority, match.Groups[1].Value.Trim()));
        }

        // Code blocks (```) - high priority
        var codeBlockMatches = Regex.Matches(content, @"^```", RegexOptions.Multiline);
        foreach (Match match in codeBlockMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.CodeBlock, 8));
        }

        // Lists (- * 1.) - medium-high priority
        var listMatches = Regex.Matches(content, @"^[\s]*[-*+]\s+", RegexOptions.Multiline);
        foreach (Match match in listMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.ListItem, 6));
        }

        // Numbered lists - medium-high priority
        var numberedListMatches = Regex.Matches(content, @"^[\s]*\d+\.\s+", RegexOptions.Multiline);
        foreach (Match match in numberedListMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.ListItem, 6));
        }

        // Double line breaks (paragraphs) - medium priority
        var paragraphMatches = Regex.Matches(content, @"\n\s*\n", RegexOptions.Multiline);
        foreach (Match match in paragraphMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.Paragraph, 5));
        }

        // Single line breaks - low priority
        var lineMatches = Regex.Matches(content, @"\n", RegexOptions.Multiline);
        foreach (Match match in lineMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.Line, 3));
        }

        return boundaries.OrderBy(b => b.Position).ToList();
    }

    private List<Boundary> DetectPdfBoundaries(string content)
    {
        var boundaries = new List<Boundary>();

        // Page breaks (if marked in content)
        var pageMatches = Regex.Matches(content, @"\f", RegexOptions.Multiline);
        foreach (Match match in pageMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.Page, 9));
        }

        // Section headers (all caps, possibly numbered)
        var sectionMatches = Regex.Matches(content, @"^[A-Z][A-Z\s\d]+$", RegexOptions.Multiline);
        foreach (Match match in sectionMatches)
        {
            if (match.Value.Length > 5 && match.Value.Length < 100)
            {
                boundaries.Add(new Boundary(match.Index, BoundaryType.Section, 7, match.Value.Trim()));
            }
        }

        // Paragraph breaks
        var paragraphMatches = Regex.Matches(content, @"\n\s*\n", RegexOptions.Multiline);
        foreach (Match match in paragraphMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.Paragraph, 5));
        }

        // Sentence endings
        var sentenceMatches = Regex.Matches(content, @"[.!?]\s+", RegexOptions.Multiline);
        foreach (Match match in sentenceMatches)
        {
            boundaries.Add(new Boundary(match.Index + match.Length - 1, BoundaryType.Sentence, 4));
        }

        return boundaries.OrderBy(b => b.Position).ToList();
    }

    private List<Boundary> DetectDocxBoundaries(string content)
    {
        var boundaries = new List<Boundary>();

        // Section breaks (if marked)
        var sectionMatches = Regex.Matches(content, @"\f", RegexOptions.Multiline);
        foreach (Match match in sectionMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.Section, 8));
        }

        // Headers (all caps, possibly numbered)
        var headerMatches = Regex.Matches(content, @"^[A-Z][A-Z\s\d]+$", RegexOptions.Multiline);
        foreach (Match match in headerMatches)
        {
            if (match.Value.Length > 5 && match.Value.Length < 100)
            {
                boundaries.Add(new Boundary(match.Index, BoundaryType.Header, 7, match.Value.Trim()));
            }
        }

        // Paragraph breaks
        var paragraphMatches = Regex.Matches(content, @"\n\s*\n", RegexOptions.Multiline);
        foreach (Match match in paragraphMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.Paragraph, 5));
        }

        // Sentence endings
        var sentenceMatches = Regex.Matches(content, @"[.!?]\s+", RegexOptions.Multiline);
        foreach (Match match in sentenceMatches)
        {
            boundaries.Add(new Boundary(match.Index + match.Length - 1, BoundaryType.Sentence, 4));
        }

        return boundaries.OrderBy(b => b.Position).ToList();
    }

    private List<Boundary> DetectTextBoundaries(string content)
    {
        var boundaries = new List<Boundary>();

        // Paragraph breaks (double line breaks)
        var paragraphMatches = Regex.Matches(content, @"\n\s*\n", RegexOptions.Multiline);
        foreach (Match match in paragraphMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.Paragraph, 5));
        }

        // Sentence endings
        var sentenceMatches = Regex.Matches(content, @"[.!?]\s+", RegexOptions.Multiline);
        foreach (Match match in sentenceMatches)
        {
            boundaries.Add(new Boundary(match.Index + match.Length - 1, BoundaryType.Sentence, 4));
        }

        // Word boundaries (spaces)
        var wordMatches = Regex.Matches(content, @"\s+", RegexOptions.Multiline);
        foreach (Match match in wordMatches)
        {
            boundaries.Add(new Boundary(match.Index, BoundaryType.Word, 2));
        }

        return boundaries.OrderBy(b => b.Position).ToList();
    }
}
