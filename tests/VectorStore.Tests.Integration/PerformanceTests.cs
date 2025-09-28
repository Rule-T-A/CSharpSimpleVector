using FluentAssertions;
using VectorStore.Core;
using VectorStore.Models;
using Xunit;

namespace VectorStore.Tests.Integration;

/// <summary>
/// Performance tests that demonstrate VectorStore performance characteristics.
/// These tests serve as both validation and performance examples.
/// </summary>
public class PerformanceTests : IDisposable
{
    private readonly string _testStorePath;
    private readonly List<string> _createdStores = new();

    public PerformanceTests()
    {
        _testStorePath = Path.Combine(Path.GetTempPath(), "VectorStorePerfTests", Guid.NewGuid().ToString("N")[..8]);
    }

    [Fact]
    public async Task LargeDataset_ShouldPerformWithinTargets()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        var documentCount = 1000;
        var documents = new List<VectorDocument>();

        // Act - Generate documents
        var generationStart = DateTime.UtcNow;
        for (int i = 0; i < documentCount; i++)
        {
            var content = GenerateTestContent(i);
            var metadata = GenerateTestMetadata(i);
            var doc = await store.CreateDocumentAsync(content, metadata);
            documents.Add(doc);
        }
        var generationTime = DateTime.UtcNow - generationStart;

        // Act - Store documents
        var storageStart = DateTime.UtcNow;
        foreach (var doc in documents)
        {
            await store.AddAsync(doc);
        }
        var storageTime = DateTime.UtcNow - storageStart;

        // Act - Search performance
        var searchStart = DateTime.UtcNow;
        var searchResults = await store.SearchTextAsync("authentication and security", limit: 10);
        var searchTime = DateTime.UtcNow - searchStart;

        // Assert - Performance targets (more realistic for embedding generation)
        var avgGenerationTime = generationTime.TotalMilliseconds / documentCount;
        var avgStorageTime = storageTime.TotalMilliseconds / documentCount;

        avgGenerationTime.Should().BeLessThan(50, "Document generation should be reasonable");
        avgStorageTime.Should().BeLessThan(20, "Document storage should be reasonable");
        searchTime.TotalMilliseconds.Should().BeLessThan(200, "Search should be reasonable even with large dataset");

        // Assert - Correctness
        searchResults.Should().NotBeNull();
        searchResults.Should().HaveCount(10);
        searchResults.Should().BeInDescendingOrder(r => r.Similarity);
    }

    [Fact]
    public async Task SearchPerformance_ShouldBeConsistent()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        // Add some test documents
        var documents = new[]
        {
            "User authentication and login functionality",
            "Database connection and data management", 
            "API endpoint security and validation",
            "User interface components and rendering",
            "Error handling and logging mechanisms"
        };

        foreach (var content in documents)
        {
            var doc = await store.CreateDocumentAsync(content, new Dictionary<string, object>());
            await store.AddAsync(doc);
        }

        // Act - Multiple searches
        var searchTimes = new List<TimeSpan>();
        var queries = new[]
        {
            "authentication and login",
            "database management",
            "API security",
            "user interface",
            "error handling"
        };

        foreach (var query in queries)
        {
            var start = DateTime.UtcNow;
            var results = await store.SearchTextAsync(query, limit: 3);
            var elapsed = DateTime.UtcNow - start;
            searchTimes.Add(elapsed);
        }

        // Assert - Performance consistency
        var avgSearchTime = searchTimes.Average(t => t.TotalMilliseconds);
        var maxSearchTime = searchTimes.Max(t => t.TotalMilliseconds);

        avgSearchTime.Should().BeLessThan(200, "Average search time should be reasonable");
        maxSearchTime.Should().BeLessThan(500, "Maximum search time should be reasonable");
        
        // All searches should return results
        foreach (var query in queries)
        {
            var results = await store.SearchTextAsync(query, limit: 3);
            results.Should().NotBeEmpty($"Search for '{query}' should return results");
        }
    }

    [Fact]
    public async Task MemoryUsage_ShouldBeReasonable()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        var documentCount = 500;
        var documents = new List<VectorDocument>();

        // Act - Add documents
        for (int i = 0; i < documentCount; i++)
        {
            var content = GenerateTestContent(i);
            var doc = await store.CreateDocumentAsync(content, new Dictionary<string, object>());
            documents.Add(doc);
            await store.AddAsync(doc);
        }

        // Force garbage collection to get accurate memory reading
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryUsage = GC.GetTotalMemory(false);
        var memoryPerDocument = memoryUsage / documentCount;

        // Assert - Memory efficiency
        memoryPerDocument.Should().BeLessThan(50_000, "Memory usage per document should be reasonable");
        
        // Verify we can still search efficiently
        var searchStart = DateTime.UtcNow;
        var results = await store.SearchTextAsync("test query", limit: 10);
        var searchTime = DateTime.UtcNow - searchStart;

        searchTime.TotalMilliseconds.Should().BeLessThan(2000, "Search should still be reasonable with reasonable memory usage");
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task PersistencePerformance_ShouldBeFast()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        // Add some documents
        for (int i = 0; i < 100; i++)
        {
            var doc = await store.CreateDocumentAsync($"Document {i}", new Dictionary<string, object>());
            await store.AddAsync(doc);
        }

        // Act - Close and reopen store (simulating persistence)
        store.Dispose();

        var openStart = DateTime.UtcNow;
        using var reopenedStore = await FileVectorStore.OpenAsync(_testStorePath);
        var openTime = DateTime.UtcNow - openStart;

        // Assert - Fast loading
        openTime.TotalMilliseconds.Should().BeLessThan(1000, "Store should load quickly from binary index");

        // Verify data integrity
        var allIds = await reopenedStore.GetAllIdsAsync();
        allIds.Should().HaveCount(100);

        // Verify search still works
        var results = await reopenedStore.SearchTextAsync("Document", limit: 5);
        results.Should().NotBeEmpty();
    }

    private static string GenerateTestContent(int index)
    {
        var categories = new[] { "authentication", "database", "security", "ui", "api", "logging", "performance", "configuration", "testing", "monitoring" };
        var actions = new[] { "login", "query", "validate", "render", "process", "log", "optimize", "configure", "test", "monitor" };
        var objects = new[] { "user", "data", "request", "component", "service", "event", "cache", "setting", "case", "metric" };

        var category = categories[index % categories.Length];
        var action = actions[index % actions.Length];
        var obj = objects[index % objects.Length];

        return $"System {category} module handles {action} operations for {obj} management with priority {index % 5 + 1}";
    }

    private static Dictionary<string, object> GenerateTestMetadata(int index)
    {
        var categories = new[] { "auth", "database", "security", "ui", "api", "logging", "performance", "config", "testing", "monitoring" };
        var priorities = new[] { "low", "medium", "high", "critical" };
        var statuses = new[] { "active", "inactive", "deprecated", "beta" };

        return new Dictionary<string, object>
        {
            ["category"] = categories[index % categories.Length],
            ["priority"] = priorities[index % priorities.Length],
            ["status"] = statuses[index % statuses.Length],
            ["index"] = index,
            ["created"] = DateTime.UtcNow.AddDays(-index % 365),
            ["version"] = $"v{(index % 10) + 1}.{(index % 5)}.{(index % 3)}"
        };
    }

    public void Dispose()
    {
        // Clean up test directories
        foreach (var storePath in _createdStores)
        {
            try
            {
                if (Directory.Exists(storePath))
                {
                    Directory.Delete(storePath, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
