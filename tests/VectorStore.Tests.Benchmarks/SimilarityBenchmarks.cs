using BenchmarkDotNet.Attributes;
using VectorStore.Search;

namespace VectorStore.Tests.Benchmarks;

/// <summary>
/// Performance benchmarks for similarity calculation operations.
/// These benchmarks help identify performance bottlenecks and measure improvements.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class SimilarityBenchmarks
{
    private float[] _queryVector = null!;
    private float[][] _vectors = null!;
    private SimilarityCalculator _calculator = null!;

    [Params(100, 1000, 10000)]
    public int VectorCount { get; set; }

    [Params(128, 512, 768)]
    public int VectorDimension { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _calculator = new SimilarityCalculator();
        
        // Generate random query vector
        _queryVector = GenerateRandomVector(VectorDimension);
        
        // Generate random vectors for batch processing
        _vectors = new float[VectorCount][];
        for (int i = 0; i < VectorCount; i++)
        {
            _vectors[i] = GenerateRandomVector(VectorDimension);
        }
    }

    [Benchmark]
    public float SingleSimilarity()
    {
        // Benchmark single similarity calculation
        var testVector = GenerateRandomVector(VectorDimension);
        return _calculator.CalculateCosineSimilarity(_queryVector, testVector);
    }

    [Benchmark]
    public float[] BatchSimilarity()
    {
        // Benchmark batch similarity calculation
        return _calculator.CalculateBatchSimilarity(_queryVector, _vectors);
    }

    [Benchmark]
    public float[] BatchSimilarity_WithSmallBatch()
    {
        // Benchmark with smaller batch size
        var smallBatch = _vectors.Take(10).ToArray();
        return _calculator.CalculateBatchSimilarity(_queryVector, smallBatch);
    }

    [Benchmark]
    public float[] BatchSimilarity_WithLargeBatch()
    {
        // Benchmark with larger batch size (if we have enough vectors)
        var largeBatch = _vectors.Take(Math.Min(1000, VectorCount)).ToArray();
        return _calculator.CalculateBatchSimilarity(_queryVector, largeBatch);
    }

    [Benchmark]
    public float[] MultipleSingleSimilarities()
    {
        // Benchmark multiple single similarity calculations (for comparison)
        var results = new float[VectorCount];
        for (int i = 0; i < VectorCount; i++)
        {
            results[i] = _calculator.CalculateCosineSimilarity(_queryVector, _vectors[i]);
        }
        return results;
    }

    private static float[] GenerateRandomVector(int dimension)
    {
        var random = new Random(42); // Fixed seed for reproducible results
        var vector = new float[dimension];
        
        for (int i = 0; i < dimension; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Random value between -1 and 1
        }
        
        // Normalize the vector
        var magnitude = Math.Sqrt(vector.Sum(x => x * x));
        for (int i = 0; i < dimension; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }
        
        return vector;
    }
}
