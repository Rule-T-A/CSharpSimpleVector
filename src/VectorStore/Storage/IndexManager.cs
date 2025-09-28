using VectorStore.Core;

namespace VectorStore.Storage;

/// <summary>
/// Manages indexes for fast lookups and searches.
/// </summary>
public class IndexManager
{
    private readonly VectorStoreOptions _options;

    public IndexManager(VectorStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    // TODO: Implement index management methods
}
