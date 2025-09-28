using VectorStore.Models;
using VectorStore.Embedding;
using VectorStore.DocumentProcessing;

namespace VectorStore.Core;

/// <summary>
/// Main interface for vector storage and similarity search operations.
/// </summary>
public interface IVectorStore : IDisposable
{
    // Storage operations
    Task<string> AddAsync(VectorDocument document);
    Task AddBatchAsync(IEnumerable<VectorDocument> documents);
    Task<bool> DeleteAsync(string id);
    Task<VectorDocument?> GetAsync(string id);
    
    // Search operations
    Task<SearchResult[]> SimilaritySearchAsync(float[] queryVector, int limit = 10);
    Task<SearchResult[]> SearchAsync(SearchQuery query);
    
    // Metadata operations
    Task<SearchResult[]> FilterAsync(Dictionary<string, object> filters, int limit = 10);
    Task<string[]> GetAllIdsAsync();
    
    // Management
    Task<StorageStats> GetStatsAsync();
    Task OptimizeAsync(); // Rebuild indexes, compact chunks
    Task ClearAsync();
    
    // Embedding operations (if enabled)
    Task<float[]> GenerateEmbeddingAsync(string text, DownloadProgressCallback? progressCallback = null);
    Task<float[][]> GenerateEmbeddingsAsync(string[] texts, DownloadProgressCallback? progressCallback = null);
    Task<VectorDocument> CreateDocumentAsync(string content, Dictionary<string, object>? metadata = null, DownloadProgressCallback? progressCallback = null);
    Task<string> AddTextAsync(string content, Dictionary<string, object>? metadata = null, DownloadProgressCallback? progressCallback = null);
    Task<SearchResult[]> SearchTextAsync(string query, int limit = 10, DownloadProgressCallback? progressCallback = null);
    Task<IEnumerable<SearchResult>> SearchTextEnumerableAsync(string query, int limit = 10, DownloadProgressCallback? progressCallback = null);
    
    // Document processing operations
    Task<DocumentParseResult> ParseDocumentAsync(string filePath, SmartChunkingOptions? options = null, DownloadProgressCallback? progressCallback = null);
    Task<string[]> AddDocumentAsync(string filePath, SmartChunkingOptions? options = null, DownloadProgressCallback? progressCallback = null);
    Task<string[]> AddDocumentAsync(string filePath, DownloadProgressCallback? progressCallback = null);
    Task<string[]> AddDocumentsAsync(string directoryPath, SmartChunkingOptions? options = null, DownloadProgressCallback? progressCallback = null);
    Task<string[]> AddDocumentsAsync(string directoryPath, DownloadProgressCallback? progressCallback = null);
}
