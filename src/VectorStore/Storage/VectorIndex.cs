using System.Collections.Concurrent;
using System.Text.Json;
using VectorStore.Models;

namespace VectorStore.Storage;

/// <summary>
/// In-memory vector index that caches embeddings with file pointers for fast similarity search.
/// </summary>
public class VectorIndex
{
    private readonly ConcurrentDictionary<string, VectorIndexEntry> _vectors = new();
    private readonly string _storePath;
    private readonly object _lock = new object();

    public VectorIndex(string storePath)
    {
        _storePath = storePath;
    }

    /// <summary>
    /// Adds a vector to the index with its file path.
    /// </summary>
    public void AddVector(string id, float[] embedding, string filePath)
    {
        _vectors[id] = new VectorIndexEntry
        {
            Id = id,
            Embedding = embedding,
            FilePath = filePath
        };
    }

    /// <summary>
    /// Removes a vector from the index.
    /// </summary>
    public bool RemoveVector(string id)
    {
        return _vectors.TryRemove(id, out _);
    }

    /// <summary>
    /// Gets all vectors for similarity search.
    /// </summary>
    public IEnumerable<VectorIndexEntry> GetAllVectors()
    {
        return _vectors.Values;
    }

    /// <summary>
    /// Gets a specific vector by ID.
    /// </summary>
    public VectorIndexEntry? GetVector(string id)
    {
        _vectors.TryGetValue(id, out var entry);
        return entry;
    }

    /// <summary>
    /// Gets the count of vectors in the index.
    /// </summary>
    public int Count => _vectors.Count;

    /// <summary>
    /// Clears all vectors from the index.
    /// </summary>
    public void Clear()
    {
        _vectors.Clear();
    }

    /// <summary>
    /// Loads all vectors from the store directory into the index.
    /// </summary>
    public async Task LoadFromStoreAsync()
    {
        if (!Directory.Exists(_storePath))
            return;

        // Try to load from binary index first (faster)
        if (await LoadFromBinaryIndexAsync())
        {
            Console.WriteLine($"✓ Loaded vector index from binary file");
            return;
        }

        // Fallback to loading from JSON files
        Console.WriteLine("Loading vector index from JSON files...");
        await LoadFromJsonFilesAsync();

        // Save the loaded index to binary for next time
        await SaveToBinaryIndexAsync();
    }

    /// <summary>
    /// Loads vectors from JSON files with corruption recovery.
    /// </summary>
    private async Task LoadFromJsonFilesAsync()
    {
        // Look for JSON files in both the root directory and documents subdirectory
        var rootJsonFiles = Directory.GetFiles(_storePath, "*.json");
        var documentsPath = Path.Combine(_storePath, "documents");
        var documentJsonFiles = Directory.Exists(documentsPath) ? Directory.GetFiles(documentsPath, "*.json") : Array.Empty<string>();
        
        var documentFiles = rootJsonFiles.Concat(documentJsonFiles).ToArray();
        var loadedCount = 0;
        var corruptedCount = 0;
        
        foreach (var filePath in documentFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                
                // Validate JSON is not empty or partial
                if (string.IsNullOrWhiteSpace(json) || !json.Trim().StartsWith("{") || !json.Trim().EndsWith("}"))
                {
                    Console.WriteLine($"Warning: Skipping partial/corrupted file: {filePath}");
                    corruptedCount++;
                    continue;
                }
                
                var document = JsonSerializer.Deserialize<VectorDocument>(json);
                
                if (document?.Embedding != null && document.Embedding.Length > 0)
                {
                    var id = Path.GetFileNameWithoutExtension(filePath);
                    AddVector(id, document.Embedding, filePath);
                    loadedCount++;
                }
                else
                {
                    Console.WriteLine($"Warning: Document {filePath} has no valid embedding, skipping");
                    corruptedCount++;
                }
            }
            catch (JsonException ex)
            {
                // Handle JSON parsing errors (corrupted files)
                Console.WriteLine($"Warning: Corrupted JSON file {filePath}: {ex.Message}");
                corruptedCount++;
            }
            catch (Exception ex)
            {
                // Handle other file errors
                Console.WriteLine($"Warning: Failed to load vector from {filePath}: {ex.Message}");
                corruptedCount++;
            }
        }
        
        Console.WriteLine($"✓ Loaded {loadedCount} vectors from JSON files (skipped {corruptedCount} corrupted files)");
    }

    /// <summary>
    /// Saves the vector index to a binary file for fast loading.
    /// </summary>
    public async Task SaveToBinaryIndexAsync()
    {
        var indexPath = Path.Combine(_storePath, "vector_index.bin");
        
        try
        {
            using var stream = new FileStream(indexPath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            
            // Write version
            writer.Write(1);
            
            // Write count
            writer.Write(_vectors.Count);
            
            // Write each vector entry
            foreach (var entry in _vectors.Values)
            {
                writer.Write(entry.Id);
                writer.Write(entry.FilePath);
                writer.Write(entry.Embedding.Length);
                foreach (var value in entry.Embedding)
                {
                    writer.Write(value);
                }
            }
            
            Console.WriteLine($"✓ Saved vector index to {indexPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to save vector index: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the vector index from a binary file.
    /// </summary>
    private async Task<bool> LoadFromBinaryIndexAsync()
    {
        var indexPath = Path.Combine(_storePath, "vector_index.bin");
        
        if (!File.Exists(indexPath))
            return false;

        try
        {
            using var stream = new FileStream(indexPath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);
            
            // Read version
            var version = reader.ReadInt32();
            if (version != 1)
                return false;
            
            // Read count
            var count = reader.ReadInt32();
            
            // Read each vector entry
            for (int i = 0; i < count; i++)
            {
                var id = reader.ReadString();
                var filePath = reader.ReadString();
                var embeddingLength = reader.ReadInt32();
                
                var embedding = new float[embeddingLength];
                for (int j = 0; j < embeddingLength; j++)
                {
                    embedding[j] = reader.ReadSingle();
                }
                
                AddVector(id, embedding, filePath);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load binary index: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads a specific document from file using the cached file path.
    /// </summary>
    public async Task<VectorDocument?> LoadDocumentAsync(string id)
    {
        var entry = GetVector(id);
        if (entry == null)
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(entry.FilePath);
            return JsonSerializer.Deserialize<VectorDocument>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load document {id} from {entry.FilePath}: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Represents a vector entry in the index with file pointer.
/// </summary>
public class VectorIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public string FilePath { get; set; } = string.Empty;
}
