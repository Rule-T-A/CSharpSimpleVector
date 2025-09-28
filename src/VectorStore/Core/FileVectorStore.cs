using Microsoft.Extensions.Logging;
using VectorStore.Models;
using VectorStore.Storage;
using VectorStore.Search;
using VectorStore.Embedding;
using VectorStore.DocumentProcessing;
using System.Text.Json;

namespace VectorStore.Core;

/// <summary>
/// File-based implementation of the vector store interface.
/// </summary>
public class FileVectorStore : IVectorStore
{
    private readonly VectorStoreOptions _options;
    private readonly ILogger<FileVectorStore>? _logger;
    private readonly IEmbeddingService? _embeddingService;
    private readonly VectorIndex _vectorIndex;
    private readonly DocumentParserFactory _parserFactory;
    private bool _disposed = false;

    /// <summary>
    /// Creates a new vector store at the specified path.
    /// </summary>
    /// <param name="storePath">The directory path for the vector store</param>
    /// <param name="options">Optional configuration options</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>A new vector store instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if the directory already exists and contains data</exception>
    public static async Task<FileVectorStore> CreateAsync(string storePath, VectorStoreOptions? options = null, ILogger<FileVectorStore>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(storePath))
            throw new ArgumentException("Store path cannot be null or empty", nameof(storePath));

        var resolvedOptions = options ?? new VectorStoreOptions();
        resolvedOptions.StorePath = storePath;

        // Check if directory exists and has data
        if (Directory.Exists(storePath))
        {
            var hasJsonFiles = Directory.GetFiles(storePath, "*.json").Length > 0;
            var hasIndexFile = File.Exists(Path.Combine(storePath, "vector_index.bin"));
            
            if (hasJsonFiles || hasIndexFile)
            {
                throw new InvalidOperationException($"Directory '{storePath}' already exists and contains vector store data. Use OpenAsync() to open an existing store.");
            }
        }

        // Create the directory
        Directory.CreateDirectory(storePath);

        var store = new FileVectorStore(resolvedOptions, logger);
        await store.LoadIndexAsync(); // This will be empty for a new store
        
        logger?.LogInformation("Created new vector store at {StorePath}", storePath);
        return store;
    }

    /// <summary>
    /// Opens an existing vector store at the specified path.
    /// </summary>
    /// <param name="storePath">The directory path for the vector store</param>
    /// <param name="options">Optional configuration options</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>An existing vector store instance</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if the directory doesn't exist</exception>
    /// <exception cref="InvalidOperationException">Thrown if the directory exists but contains no vector store data</exception>
    public static async Task<FileVectorStore> OpenAsync(string storePath, VectorStoreOptions? options = null, ILogger<FileVectorStore>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(storePath))
            throw new ArgumentException("Store path cannot be null or empty", nameof(storePath));

        if (!Directory.Exists(storePath))
        {
            throw new DirectoryNotFoundException($"Vector store directory '{storePath}' does not exist. Use CreateAsync() to create a new store.");
        }

        var resolvedOptions = options ?? new VectorStoreOptions();
        resolvedOptions.StorePath = storePath;

        var store = new FileVectorStore(resolvedOptions, logger);
        await store.LoadIndexAsync();

        // Verify we loaded some data
        if (store._vectorIndex.Count == 0)
        {
            var hasJsonFiles = Directory.GetFiles(storePath, "*.json").Length > 0;
            if (!hasJsonFiles)
            {
                throw new InvalidOperationException($"Directory '{storePath}' exists but contains no vector store data. Use CreateAsync() to create a new store.");
            }
        }

        logger?.LogInformation("Opened existing vector store at {StorePath} with {Count} documents", storePath, store._vectorIndex.Count);
        return store;
    }

    /// <summary>
    /// Deletes a vector store and all its data.
    /// </summary>
    /// <param name="storePath">The directory path for the vector store</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>True if the store was deleted, false if it didn't exist</returns>
    public static async Task<bool> DeleteAsync(string storePath, ILogger<FileVectorStore>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(storePath))
            throw new ArgumentException("Store path cannot be null or empty", nameof(storePath));

        if (!Directory.Exists(storePath))
        {
            logger?.LogWarning("Vector store directory '{StorePath}' does not exist", storePath);
            return false;
        }

        try
        {
            // Check if it's actually a vector store
            var hasJsonFiles = Directory.GetFiles(storePath, "*.json").Length > 0;
            var hasIndexFile = File.Exists(Path.Combine(storePath, "vector_index.bin"));
            
            if (!hasJsonFiles && !hasIndexFile)
            {
                logger?.LogWarning("Directory '{StorePath}' exists but is not a vector store", storePath);
                return false;
            }

            // Delete the entire directory
            Directory.Delete(storePath, true);
            
            logger?.LogInformation("Successfully deleted vector store at {StorePath}", storePath);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to delete vector store at {StorePath}", storePath);
            throw;
        }
    }

    private FileVectorStore(VectorStoreOptions options, ILogger<FileVectorStore>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        
        // Initialize vector index for fast similarity search
        _vectorIndex = new VectorIndex(options.StorePath);
        
        // Initialize document parser factory
        _parserFactory = new DocumentParserFactory();
        
        // Initialize embedding service if enabled
        if (options.EnableEmbeddingGeneration)
        {
            _embeddingService = new NomicEmbeddingService();
        }
    }

    /// <summary>
    /// Loads the vector index from existing files in the store.
    /// Call this after construction to populate the index with existing documents.
    /// </summary>
    public async Task LoadIndexAsync()
    {
        await _vectorIndex.LoadFromStoreAsync();
        _logger?.LogDebug("Vector index loaded with {Count} vectors", _vectorIndex.Count);
    }

    public async Task<string> AddAsync(VectorDocument document)
    {
        // Ensure store directory exists
        Directory.CreateDirectory(_options.StorePath);
        
        // Save document to JSON file
        var documentPath = Path.Combine(_options.StorePath, $"{document.Id}.json");
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(documentPath, json);
        
        // Add vector to index for fast similarity search
        if (document.Embedding != null && document.Embedding.Length > 0)
        {
            _vectorIndex.AddVector(document.Id, document.Embedding, documentPath);
            
            // Save the updated index to binary file
            await _vectorIndex.SaveToBinaryIndexAsync();
        }
        
        _logger?.LogDebug("Document {Id} saved to {Path}", document.Id, documentPath);
        return document.Id;
    }

    public Task AddBatchAsync(IEnumerable<VectorDocument> documents)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteAsync(string id)
    {
        var documentPath = Path.Combine(_options.StorePath, $"{id}.json");
        
        if (!File.Exists(documentPath))
        {
            _logger?.LogDebug("Document {Id} not found for deletion", id);
            return Task.FromResult(false);
        }
        
        try
        {
            File.Delete(documentPath);
            
            // Remove vector from index
            _vectorIndex.RemoveVector(id);
            
            // Save the updated index to binary file
            _ = Task.Run(async () => await _vectorIndex.SaveToBinaryIndexAsync());
            
            _logger?.LogDebug("Document {Id} deleted from {Path}", id, documentPath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete document {Id} from {Path}", id, documentPath);
            return Task.FromResult(false);
        }
    }

    public async Task<VectorDocument?> GetAsync(string id)
    {
        // Try to load from vector index first (faster)
        var document = await _vectorIndex.LoadDocumentAsync(id);
        if (document != null)
        {
            _logger?.LogDebug("Document {Id} loaded from vector index", id);
            return document;
        }
        
        // Fallback to direct file access
        var documentPath = Path.Combine(_options.StorePath, $"{id}.json");
        
        if (!File.Exists(documentPath))
        {
            _logger?.LogDebug("Document {Id} not found at {Path}", id, documentPath);
            return null;
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(documentPath);
            var doc = JsonSerializer.Deserialize<VectorDocument>(json);
            _logger?.LogDebug("Document {Id} loaded from {Path}", id, documentPath);
            return doc;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load document {Id} from {Path}", id, documentPath);
            return null;
        }
    }

    public async Task<SearchResult[]> SimilaritySearchAsync(float[] queryVector, int limit = 10)
    {
        var results = new List<(string Id, float Similarity)>();
        
        // Use vector index for fast similarity search
        var vectors = _vectorIndex.GetAllVectors();
        
        foreach (var vectorEntry in vectors)
        {
            try
            {
                var similarity = CalculateCosineSimilarity(queryVector, vectorEntry.Embedding);
                results.Add((vectorEntry.Id, similarity));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to calculate similarity for vector {Id}", vectorEntry.Id);
            }
        }
        
        // Sort by similarity (highest first) and take top results
        var topResults = results
            .OrderByDescending(r => r.Similarity)
            .Take(limit)
            .ToArray();
        
        // Load full documents for top results
        var searchResults = new List<SearchResult>();
        foreach (var (id, similarity) in topResults)
        {
            var document = await _vectorIndex.LoadDocumentAsync(id);
            if (document != null)
            {
                searchResults.Add(new SearchResult
                {
                    Document = document,
                    Similarity = similarity
                });
            }
        }
        
        return searchResults.ToArray();
    }
    
    private float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException("Vectors must have the same length");
        
        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;
        
        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }
        
        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);
        
        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;
        
        return dotProduct / (magnitudeA * magnitudeB);
    }

    public Task<SearchResult[]> SearchAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<SearchResult[]> FilterAsync(Dictionary<string, object> filters, int limit = 10)
    {
        throw new NotImplementedException();
    }

    public Task<string[]> GetAllIdsAsync()
    {
        // Use vector index for fast ID retrieval
        var ids = _vectorIndex.GetAllVectors()
            .Select(v => v.Id)
            .ToArray();
        
        _logger?.LogDebug("Found {Count} documents in store", ids.Length);
        return Task.FromResult(ids);
    }

    public Task<StorageStats> GetStatsAsync()
    {
        throw new NotImplementedException();
    }

    public Task OptimizeAsync()
    {
        throw new NotImplementedException();
    }

    public Task ClearAsync()
    {
        throw new NotImplementedException();
    }

    // Embedding operations
    public async Task<float[]> GenerateEmbeddingAsync(string text, DownloadProgressCallback? progressCallback = null)
    {
        if (_embeddingService == null)
            throw new InvalidOperationException("Embedding generation is not enabled. Set EnableEmbeddingGeneration to true in options.");
        
        return await _embeddingService.GenerateEmbeddingAsync(text, progressCallback);
    }

    public async Task<float[][]> GenerateEmbeddingsAsync(string[] texts, DownloadProgressCallback? progressCallback = null)
    {
        if (_embeddingService == null)
            throw new InvalidOperationException("Embedding generation is not enabled. Set EnableEmbeddingGeneration to true in options.");
        
        return await _embeddingService.GenerateEmbeddingsAsync(texts, progressCallback);
    }

    public async Task<VectorDocument> CreateDocumentAsync(string content, Dictionary<string, object>? metadata = null, DownloadProgressCallback? progressCallback = null)
    {
        if (_embeddingService == null)
            throw new InvalidOperationException("Embedding generation is not enabled. Set EnableEmbeddingGeneration to true in options.");
        
        var embedding = await _embeddingService.GenerateEmbeddingAsync(content, progressCallback);
        
        return new VectorDocument
        {
            Id = Guid.NewGuid().ToString(),
            Content = content,
            Embedding = embedding,
            Metadata = metadata ?? new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<string> AddTextAsync(string content, Dictionary<string, object>? metadata = null, DownloadProgressCallback? progressCallback = null)
    {
        var document = await CreateDocumentAsync(content, metadata, progressCallback);
        return await AddAsync(document);
    }

    public async Task<SearchResult[]> SearchTextAsync(string query, int limit = 10, DownloadProgressCallback? progressCallback = null)
    {
        if (_embeddingService == null)
            throw new InvalidOperationException("Embedding generation is not enabled. Set EnableEmbeddingGeneration to true in options.");
        
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query, progressCallback);
        return await SimilaritySearchAsync(queryVector, limit);
    }

    public async Task<IEnumerable<SearchResult>> SearchTextEnumerableAsync(string query, int limit = 10, DownloadProgressCallback? progressCallback = null)
    {
        if (_embeddingService == null)
            throw new InvalidOperationException("Embedding generation is not enabled. Set EnableEmbeddingGeneration to true in options.");
        
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query, progressCallback);
        var results = await SimilaritySearchAsync(queryVector, limit);
        return results.AsEnumerable();
    }

    // Document processing operations
    public async Task<DocumentParseResult> ParseDocumentAsync(string filePath, SmartChunkingOptions? options = null, DownloadProgressCallback? progressCallback = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var parser = _parserFactory.GetParser(filePath);
        if (parser == null)
            throw new NotSupportedException($"File type not supported: {Path.GetExtension(filePath)}");

        return await parser.ParseAsync(filePath, options);
    }

    public async Task<string[]> AddDocumentAsync(string filePath, SmartChunkingOptions? options = null, DownloadProgressCallback? progressCallback = null)
    {
        if (_embeddingService == null)
            throw new InvalidOperationException("Embedding generation is not enabled. Set EnableEmbeddingGeneration to true in options.");

        var parseResult = await ParseDocumentAsync(filePath, options, progressCallback);
        var documentIds = new List<string>();

        foreach (var chunk in parseResult.Chunks)
        {
            // Create metadata for this chunk
            var chunkMetadata = new Dictionary<string, object>(parseResult.Metadata);
            foreach (var kvp in chunk.Metadata)
            {
                chunkMetadata[kvp.Key] = kvp.Value;
            }

            // Add source document information
            chunkMetadata["source_file"] = parseResult.FilePath;
            chunkMetadata["source_title"] = parseResult.Title;
            chunkMetadata["chunk_index"] = chunk.ChunkIndex;
            chunkMetadata["total_chunks"] = parseResult.TotalChunks;

            // Create and add the document
            var document = await CreateDocumentAsync(chunk.Content, chunkMetadata, progressCallback);
            var documentId = await AddAsync(document);
            documentIds.Add(documentId);
        }

        return documentIds.ToArray();
    }

    public async Task<string[]> AddDocumentAsync(string filePath, DownloadProgressCallback? progressCallback = null)
    {
        return await AddDocumentAsync(filePath, null, progressCallback);
    }

    public async Task<string[]> AddDocumentsAsync(string directoryPath, SmartChunkingOptions? options = null, DownloadProgressCallback? progressCallback = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var allDocumentIds = new List<string>();
        var supportedFiles = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(f => _parserFactory.CanParse(f))
            .ToArray();

        _logger?.LogInformation("Found {Count} supported files in directory {DirectoryPath}", supportedFiles.Length, directoryPath);

        foreach (var filePath in supportedFiles)
        {
            try
            {
                _logger?.LogDebug("Processing file: {FilePath}", filePath);
                var documentIds = await AddDocumentAsync(filePath, options, progressCallback);
                allDocumentIds.AddRange(documentIds);
                
                _logger?.LogDebug("Added {ChunkCount} chunks from {FilePath}", documentIds.Length, filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to process file: {FilePath}", filePath);
                // Continue processing other files
            }
        }

        return allDocumentIds.ToArray();
    }

    public async Task<string[]> AddDocumentsAsync(string directoryPath, DownloadProgressCallback? progressCallback = null)
    {
        return await AddDocumentsAsync(directoryPath, null, progressCallback);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // TODO: Cleanup resources
            _embeddingService?.Dispose();
            _disposed = true;
        }
    }
}
