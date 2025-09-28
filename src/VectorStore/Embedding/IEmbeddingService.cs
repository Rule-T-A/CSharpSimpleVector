namespace VectorStore.Embedding;

/// <summary>
/// Progress callback for model download operations.
/// </summary>
/// <param name="bytesDownloaded">Number of bytes downloaded so far</param>
/// <param name="totalBytes">Total number of bytes to download</param>
/// <param name="percentage">Download percentage (0-100)</param>
public delegate void DownloadProgressCallback(long bytesDownloaded, long totalBytes, double percentage);

/// <summary>
/// Interface for generating text embeddings.
/// </summary>
public interface IEmbeddingService : IDisposable
{
    /// <summary>
    /// Generates an embedding for the given text.
    /// </summary>
    /// <param name="text">The text to embed</param>
    /// <param name="progressCallback">Optional callback for download progress</param>
    /// <returns>The embedding vector</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, DownloadProgressCallback? progressCallback = null);

    /// <summary>
    /// Generates embeddings for multiple texts in batch.
    /// </summary>
    /// <param name="texts">The texts to embed</param>
    /// <param name="progressCallback">Optional callback for download progress</param>
    /// <returns>Array of embedding vectors</returns>
    Task<float[][]> GenerateEmbeddingsAsync(string[] texts, DownloadProgressCallback? progressCallback = null);

    /// <summary>
    /// Gets the dimension size of the embeddings produced by this service.
    /// </summary>
    int EmbeddingDimensions { get; }

    /// <summary>
    /// Gets the model name being used.
    /// </summary>
    string ModelName { get; }
}
