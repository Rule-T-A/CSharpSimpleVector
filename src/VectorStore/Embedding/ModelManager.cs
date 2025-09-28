using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace VectorStore.Embedding;

/// <summary>
/// Manages downloading and caching of Nomic models from HuggingFace.
/// </summary>
public class ModelManager
{
    private readonly ILogger<ModelManager>? _logger;
    private readonly string _modelsPath;
    private const string NOMIC_MODEL_ID = "nomic-ai/nomic-embed-text-v1";
    private const string MODEL_FILENAME = "model.onnx";

    public ModelManager(ILogger<ModelManager>? logger = null)
    {
        _logger = logger;
        _modelsPath = Path.Combine(GetUserDataPath(), "models", "nomic");
    }

    /// <summary>
    /// Ensures the Nomic model is downloaded and available locally.
    /// </summary>
    public async Task<string> EnsureModelAvailableAsync(DownloadProgressCallback? progressCallback = null)
    {
        var modelPath = Path.Combine(_modelsPath, MODEL_FILENAME);
        
        Console.WriteLine($"🔍 DEBUG: Checking for model at {modelPath}");
        Console.WriteLine($"🔍 DEBUG: File exists: {File.Exists(modelPath)}");
        
        if (File.Exists(modelPath))
        {
            Console.WriteLine($"✅ DEBUG: Model already exists, skipping download");
            _logger?.LogDebug("Nomic model already available at {ModelPath}", modelPath);
            return modelPath;
        }

        Console.WriteLine($"📥 DEBUG: Model not found, starting download...");
        _logger?.LogInformation("Downloading Nomic model from HuggingFace...");
        
        // Ensure directory exists
        Directory.CreateDirectory(_modelsPath);
        Console.WriteLine($"📁 DEBUG: Created directory: {_modelsPath}");

        try
        {
            // Download the actual Nomic model from HuggingFace
            await DownloadNomicModelAsync(modelPath, progressCallback);
            
            Console.WriteLine($"✅ DEBUG: Download completed successfully");
            _logger?.LogInformation("Nomic model downloaded successfully to {ModelPath}", modelPath);
            return modelPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DEBUG: Download failed with exception: {ex.Message}");
            _logger?.LogError(ex, "Failed to download Nomic model");
            throw new InvalidOperationException("Failed to download Nomic model", ex);
        }
    }

    private async Task DownloadNomicModelAsync(string modelPath, DownloadProgressCallback? progressCallback)
    {
        using var httpClient = new HttpClient();
        
        // Download the ONNX model file from HuggingFace
        var modelUrl = "https://huggingface.co/nomic-ai/nomic-embed-text-v1/resolve/main/onnx/model.onnx";
        
        Console.WriteLine($"🔍 DEBUG: Attempting to download model from {modelUrl}");
        _logger?.LogInformation("Downloading model from {ModelUrl}", modelUrl);
        
        try
        {
            using var response = await httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead);
            Console.WriteLine($"🔍 DEBUG: HTTP Response Status: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ DEBUG: Model download failed with status {response.StatusCode}");
                throw new HttpRequestException($"Failed to download model: {response.StatusCode}");
            }
            
            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            Console.WriteLine($"🔍 DEBUG: Total bytes to download: {totalBytes:N0}");
            
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            
            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            int bytesRead;
            
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                
                // Report progress
                if (progressCallback != null && totalBytes > 0)
                {
                    var percentage = (double)totalBytesRead / totalBytes * 100;
                    progressCallback(totalBytesRead, totalBytes, percentage);
                }
                
                // Also log progress every 10%
                if (totalBytes > 0 && totalBytesRead % (totalBytes / 10) < bytesRead)
                {
                    var percentage = (double)totalBytesRead / totalBytes * 100;
                    Console.WriteLine($"📥 DEBUG: Download progress: {percentage:F1}% ({totalBytesRead:N0}/{totalBytes:N0} bytes)");
                }
            }
            
            Console.WriteLine($"✅ DEBUG: Model saved to {modelPath}");
            Console.WriteLine($"🔍 DEBUG: Downloaded {totalBytesRead:N0} bytes total");
            _logger?.LogInformation("Model downloaded successfully ({Size} bytes)", totalBytesRead);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DEBUG: Download failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the user data directory for caching models and embeddings.
    /// </summary>
    private static string GetUserDataPath()
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homePath, ".vectorstore");
    }

    /// <summary>
    /// Gets the cache directory for embeddings.
    /// </summary>
    public string GetEmbeddingCachePath()
    {
        return Path.Combine(GetUserDataPath(), "cache", "embeddings");
    }
}
