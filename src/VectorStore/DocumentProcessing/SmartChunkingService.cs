using System.Text;
using VectorStore.Models;

namespace VectorStore.DocumentProcessing;

/// <summary>
/// Service for creating intelligent chunks from document content.
/// </summary>
public class SmartChunkingService
{
    private readonly BoundaryDetector _boundaryDetector;

    public SmartChunkingService()
    {
        _boundaryDetector = new BoundaryDetector();
    }

    /// <summary>
    /// Creates smart chunks from content using the specified options.
    /// </summary>
    public List<DocumentChunk> CreateSmartChunks(string content, DocumentType type, SmartChunkingOptions options)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new List<DocumentChunk>();

        // Detect boundaries
        var boundaries = _boundaryDetector.DetectBoundaries(content, type);
        
        // Create chunks using boundaries
        return CreateChunksFromBoundaries(content, boundaries, options);
    }

    private List<DocumentChunk> CreateChunksFromBoundaries(string content, List<Boundary> boundaries, SmartChunkingOptions options)
    {
        var chunks = new List<DocumentChunk>();
        var currentChunk = new StringBuilder();
        var currentPosition = 0;
        var chunkIndex = 0;
        var lastOverlap = string.Empty;

        foreach (var boundary in boundaries)
        {
            var segment = content.Substring(currentPosition, boundary.Position - currentPosition);
            var potentialChunk = currentChunk.ToString() + segment;

            // Check if adding this segment would exceed max size
            if (potentialChunk.Length > options.MaxChunkSize)
            {
                // Find the best stopping point within the current content
                var bestStopPoint = FindBestStopPoint(currentChunk.ToString(), segment, options, boundaries, currentPosition);
                
                if (bestStopPoint > 0)
                {
                    // Create chunk up to the best stop point
                    var chunkContent = currentChunk.ToString() + segment.Substring(0, bestStopPoint);
                    var finalChunk = CreateChunk(chunkContent, currentPosition, chunkIndex++, lastOverlap);
                    
                    if (finalChunk.Content.Length >= options.MinChunkSize)
                    {
                        chunks.Add(finalChunk);
                        lastOverlap = GetSmartOverlap(chunkContent, options.OverlapSize);
                    }

                    // Start new chunk with overlap
                    currentChunk.Clear();
                    currentChunk.Append(lastOverlap);
                    currentPosition += bestStopPoint;
                }
                else
                {
                    // Force create chunk if we can't find a good stopping point
                    var chunkContent = currentChunk.ToString();
                    if (chunkContent.Length >= options.MinChunkSize)
                    {
                        chunks.Add(CreateChunk(chunkContent, currentPosition, chunkIndex++, lastOverlap));
                        lastOverlap = GetSmartOverlap(chunkContent, options.OverlapSize);
                    }

                    currentChunk.Clear();
                    currentChunk.Append(lastOverlap);
                    currentPosition = boundary.Position;
                }
            }
            else
            {
                currentChunk.Append(segment);
                currentPosition = boundary.Position;
            }
        }

        // Handle remaining content
        if (currentChunk.Length > 0)
        {
            var remainingContent = currentChunk.ToString();
            if (remainingContent.Length >= options.MinChunkSize)
            {
                chunks.Add(CreateChunk(remainingContent, currentPosition, chunkIndex, lastOverlap));
            }
        }

        // If no chunks were created (content too small), create one chunk anyway
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(content))
        {
            chunks.Add(CreateChunk(content, 0, 0, string.Empty));
        }

        return chunks;
    }

    private int FindBestStopPoint(string currentContent, string segment, SmartChunkingOptions options, List<Boundary> boundaries, int currentPosition)
    {
        var totalLength = currentContent.Length + segment.Length;
        
        if (totalLength <= options.MaxChunkSize)
            return segment.Length;

        // Look for boundaries within the segment
        var relevantBoundaries = boundaries
            .Where(b => b.Position >= currentPosition && b.Position < currentPosition + segment.Length)
            .OrderByDescending(b => b.Priority)
            .ThenBy(b => Math.Abs(b.Position - (currentPosition + options.MaxChunkSize - currentContent.Length)));

        foreach (var boundary in relevantBoundaries)
        {
            var stopPoint = boundary.Position - currentPosition;
            if (stopPoint > 0 && stopPoint < segment.Length)
            {
                var chunkLength = currentContent.Length + stopPoint;
                if (chunkLength >= options.MinChunkSize && chunkLength <= options.MaxChunkSize)
                {
                    return stopPoint;
                }
            }
        }

        // Fallback: find a reasonable stopping point
        var targetLength = options.MaxChunkSize - currentContent.Length;
        if (targetLength <= 0)
            return 0;

        // Try to stop at a sentence boundary
        var sentenceEnd = segment.LastIndexOfAny(new[] { '.', '!', '?' });
        if (sentenceEnd > 0 && sentenceEnd < targetLength)
            return sentenceEnd + 1;

        // Try to stop at a word boundary
        var wordEnd = segment.LastIndexOf(' ', targetLength);
        if (wordEnd > 0)
            return wordEnd;

        // Last resort: stop at target length
        return Math.Min(targetLength, segment.Length);
    }

    private DocumentChunk CreateChunk(string content, int startPosition, int chunkIndex, string overlap)
    {
        // Remove leading overlap if present
        if (!string.IsNullOrEmpty(overlap) && content.StartsWith(overlap))
        {
            content = content.Substring(overlap.Length).TrimStart();
        }

        return new DocumentChunk
        {
            Content = content.Trim(),
            ChunkIndex = chunkIndex,
            StartPosition = startPosition,
            EndPosition = startPosition + content.Length,
            Metadata = new Dictionary<string, object>
            {
                ["word_count"] = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                ["character_count"] = content.Length,
                ["has_overlap"] = !string.IsNullOrEmpty(overlap)
            }
        };
    }

    private string GetSmartOverlap(string chunkContent, int overlapSize)
    {
        if (chunkContent.Length <= overlapSize)
            return chunkContent;

        var searchText = chunkContent.Substring(Math.Max(0, chunkContent.Length - overlapSize * 2));

        // Try to find a sentence boundary
        var lastSentence = FindLastSentence(searchText);
        if (lastSentence.Length > overlapSize / 2)
            return lastSentence.Trim();

        // Try to find a word boundary
        var lastWord = FindLastWord(searchText);
        if (lastWord.Length > overlapSize / 3)
            return lastWord.Trim();

        // Last resort: character boundary
        return searchText.Substring(Math.Max(0, searchText.Length - overlapSize)).Trim();
    }

    private string FindLastSentence(string text)
    {
        var sentenceEnd = text.LastIndexOfAny(new[] { '.', '!', '?' });
        if (sentenceEnd > 0)
        {
            return text.Substring(0, sentenceEnd + 1);
        }
        return string.Empty;
    }

    private string FindLastWord(string text)
    {
        var lastSpace = text.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            return text.Substring(0, lastSpace);
        }
        return string.Empty;
    }
}
