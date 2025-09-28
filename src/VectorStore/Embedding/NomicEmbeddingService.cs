using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text;

namespace VectorStore.Embedding;

/// <summary>
/// Nomic embedding service using local ONNX model inference.
/// </summary>
public class NomicEmbeddingService : IEmbeddingService
{
    private readonly ILogger<NomicEmbeddingService>? _logger;
    private readonly EmbeddingCache _cache;
    private readonly ModelManager _modelManager;
    private InferenceSession? _session;
    private bool _disposed = false;

    public int EmbeddingDimensions => 768;
    public string ModelName => "nomic-ai/nomic-embed-text-v1";

    public NomicEmbeddingService(ILogger<NomicEmbeddingService>? logger = null)
    {
        _logger = logger;
        _modelManager = new ModelManager();
        _cache = new EmbeddingCache(_modelManager.GetEmbeddingCachePath());
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, DownloadProgressCallback? progressCallback = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or empty", nameof(text));

        Console.WriteLine($"🔍 DEBUG: GenerateEmbeddingAsync called for text: '{text.Substring(0, Math.Min(50, text.Length))}...'");

        // Check cache first
        var cached = await _cache.GetAsync(text);
        if (cached != null)
        {
            Console.WriteLine($"✅ DEBUG: Using cached embedding");
            _logger?.LogDebug("Using cached embedding for text");
            return cached;
        }

        Console.WriteLine($"📥 DEBUG: No cached embedding found, generating new embedding...");
        // Ensure model is available
        await EnsureModelLoadedAsync(progressCallback);

        // Generate embedding
        var embedding = await GenerateEmbeddingInternalAsync(text);
        
        // Cache the result
        await _cache.SetAsync(text, embedding);
        Console.WriteLine($"💾 DEBUG: Embedding cached for future use");
        
        return embedding;
    }

    public async Task<float[][]> GenerateEmbeddingsAsync(string[] texts, DownloadProgressCallback? progressCallback = null)
    {
        if (texts == null || texts.Length == 0)
            throw new ArgumentException("Texts cannot be null or empty", nameof(texts));

        var results = new float[texts.Length][];
        var uncachedTexts = new List<(int index, string text)>();
        var uncachedIndices = new List<int>();

        // Check cache for all texts
        for (int i = 0; i < texts.Length; i++)
        {
            var cached = await _cache.GetAsync(texts[i]);
            if (cached != null)
            {
                results[i] = cached;
            }
            else
            {
                uncachedTexts.Add((i, texts[i]));
                uncachedIndices.Add(i);
            }
        }

        // Generate embeddings for uncached texts
        if (uncachedTexts.Count > 0)
        {
            await EnsureModelLoadedAsync(progressCallback);
            
            for (int i = 0; i < uncachedTexts.Count; i++)
            {
                var (originalIndex, text) = uncachedTexts[i];
                var embedding = await GenerateEmbeddingInternalAsync(text);
                results[originalIndex] = embedding;
                
                // Cache the result
                await _cache.SetAsync(text, embedding);
            }
        }

        return results;
    }

    private async Task EnsureModelLoadedAsync(DownloadProgressCallback? progressCallback = null)
    {
        if (_session != null) return;

        _logger?.LogDebug("Loading Nomic model...");
        
        var modelPath = await _modelManager.EnsureModelAvailableAsync(progressCallback);
        
        try
        {
            _session = new InferenceSession(modelPath);
            _logger?.LogInformation("Nomic model loaded successfully from {ModelPath}", modelPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load Nomic model from {ModelPath}", modelPath);
            throw new InvalidOperationException("Failed to load Nomic model", ex);
        }
    }

    private async Task<float[]> GenerateEmbeddingInternalAsync(string text)
    {
        if (_session == null)
            throw new InvalidOperationException("Model not loaded");

        try
        {
            // Tokenize the text (simplified tokenization for now)
            var tokens = TokenizeText(text);
            
            // Create input tensors for the ONNX model
            var inputIds = new DenseTensor<long>(new[] { 1, tokens.Length });
            var attentionMask = new DenseTensor<long>(new[] { 1, tokens.Length });
            var tokenTypeIds = new DenseTensor<long>(new[] { 1, tokens.Length });
            
            for (int i = 0; i < tokens.Length; i++)
            {
                inputIds[0, i] = tokens[i];
                attentionMask[0, i] = 1;
                tokenTypeIds[0, i] = 0; // All tokens are type 0
            }

            // Prepare inputs for ONNX inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
            };

            // Run inference
            using var results = _session.Run(inputs);
            var outputTensor = results.First().AsTensor<float>();
            
            // Extract the embedding (last hidden state, mean pooled)
            var embedding = new float[EmbeddingDimensions];
            var sequenceLength = outputTensor.Dimensions[1];
            
            // Mean pooling over the sequence length
            for (int i = 0; i < EmbeddingDimensions; i++)
            {
                float sum = 0;
                for (int j = 0; j < sequenceLength; j++)
                {
                    sum += outputTensor[0, j, i];
                }
                embedding[i] = sum / sequenceLength;
            }
            
            // Normalize the embedding
            var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
            if (magnitude > 0)
            {
                for (int i = 0; i < embedding.Length; i++)
                {
                    embedding[i] = embedding[i] / magnitude;
                }
            }
            
            _logger?.LogDebug("Generated embedding for text of length {TextLength}", text.Length);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate embedding for text");
            throw;
        }
    }

    private long[] TokenizeText(string text)
    {
        // Simplified tokenization - in a real implementation, you'd use the actual Nomic tokenizer
        // For now, we'll create a basic word-based tokenization
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':' }, 
                   StringSplitOptions.RemoveEmptyEntries);
        
        var tokens = new List<long>();
        
        // Add special tokens
        tokens.Add(101); // [CLS] token
        
        // Add word tokens (simplified mapping)
        foreach (var word in words.Take(510)) // Limit to reasonable sequence length
        {
            var tokenId = Math.Abs(word.GetHashCode()) % 30000 + 2; // Simple hash-based tokenization
            tokens.Add(tokenId);
        }
        
        tokens.Add(102); // [SEP] token
        
        return tokens.ToArray();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session?.Dispose();
            _cache.Dispose();
            _disposed = true;
        }
    }
}
