using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VectorStore.Embedding;

/// <summary>
/// Manages caching of embeddings with both file-based and in-memory storage.
/// </summary>
public class EmbeddingCache : IDisposable
{
    private readonly ILogger<EmbeddingCache>? _logger;
    private readonly string _cachePath;
    private readonly ConcurrentDictionary<string, float[]> _memoryCache;
    private readonly int _maxMemoryItems;
    private bool _disposed = false;

    public EmbeddingCache(string cachePath, int maxMemoryItems = 1000, ILogger<EmbeddingCache>? logger = null)
    {
        _logger = logger;
        _cachePath = cachePath;
        _maxMemoryItems = maxMemoryItems;
        _memoryCache = new ConcurrentDictionary<string, float[]>();
        
        // Ensure cache directory exists
        Directory.CreateDirectory(_cachePath);
    }

    /// <summary>
    /// Gets an embedding from cache (memory first, then file).
    /// </summary>
    public async Task<float[]?> GetAsync(string text)
    {
        var key = GetCacheKey(text);
        
        // Try memory cache first
        if (_memoryCache.TryGetValue(key, out var cachedEmbedding))
        {
            _logger?.LogDebug("Embedding found in memory cache for text hash {Key}", key);
            return cachedEmbedding;
        }

        // Try file cache
        var filePath = Path.Combine(_cachePath, $"{key}.json");
        if (File.Exists(filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var embedding = JsonSerializer.Deserialize<float[]>(json);
                
                if (embedding != null)
                {
                    // Add to memory cache
                    AddToMemoryCache(key, embedding);
                    _logger?.LogDebug("Embedding loaded from file cache for text hash {Key}", key);
                    return embedding;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load embedding from file cache for key {Key}", key);
            }
        }

        return null;
    }

    /// <summary>
    /// Stores an embedding in both memory and file cache.
    /// </summary>
    public async Task SetAsync(string text, float[] embedding)
    {
        var key = GetCacheKey(text);
        
        // Add to memory cache
        AddToMemoryCache(key, embedding);
        
        // Save to file cache
        var filePath = Path.Combine(_cachePath, $"{key}.json");
        try
        {
            var json = JsonSerializer.Serialize(embedding);
            await File.WriteAllTextAsync(filePath, json);
            _logger?.LogDebug("Embedding cached to file for text hash {Key}", key);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save embedding to file cache for key {Key}", key);
        }
    }

    /// <summary>
    /// Adds an embedding to memory cache with LRU eviction.
    /// </summary>
    private void AddToMemoryCache(string key, float[] embedding)
    {
        // Simple LRU: if we're at capacity, remove oldest item
        if (_memoryCache.Count >= _maxMemoryItems)
        {
            var oldestKey = _memoryCache.Keys.First();
            _memoryCache.TryRemove(oldestKey, out _);
        }
        
        _memoryCache[key] = embedding;
    }

    /// <summary>
    /// Generates a cache key for the given text.
    /// </summary>
    private static string GetCacheKey(string text)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _memoryCache.Clear();
            _disposed = true;
        }
    }
}
