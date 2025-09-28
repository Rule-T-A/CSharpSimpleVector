using VectorStore.Core;

namespace VectorStore.Storage;

/// <summary>
/// Manages the lifecycle of embedding and metadata chunks.
/// </summary>
public class ChunkManager
{
    private readonly VectorStoreOptions _options;

    public ChunkManager(VectorStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    // TODO: Implement chunk management methods
}
