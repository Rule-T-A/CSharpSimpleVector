using FluentAssertions;
using VectorStore.Core;
using VectorStore.Models;
using Xunit;

namespace VectorStore.Tests.Integration;

/// <summary>
/// End-to-end tests that demonstrate basic VectorStore usage patterns.
/// These tests serve as both validation and usage examples.
/// </summary>
public class EndToEndTests : IDisposable
{
    private readonly string _testStorePath;
    private readonly List<string> _createdStores = new();

    public EndToEndTests()
    {
        _testStorePath = Path.Combine(Path.GetTempPath(), "VectorStoreTests", Guid.NewGuid().ToString("N")[..8]);
    }

    [Fact]
    public async Task CreateStore_ShouldCreateNewEmptyStore()
    {
        // Arrange & Act
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        // Assert
        store.Should().NotBeNull();
        Directory.Exists(_testStorePath).Should().BeTrue();
        File.Exists(Path.Combine(_testStorePath, "vector_index.bin")).Should().BeTrue();
    }

    [Fact]
    public async Task OpenStore_ShouldLoadExistingStore()
    {
        // Arrange - Create a store with some data
        using var createdStore = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);
        
        var docId = await createdStore.AddTextAsync("Test document", new Dictionary<string, object> { ["test"] = "value" });

        // Act - Open the existing store
        using var openedStore = await FileVectorStore.OpenAsync(_testStorePath);

        // Assert
        openedStore.Should().NotBeNull();
        var allIds = await openedStore.GetAllIdsAsync();
        allIds.Should().HaveCount(1);
        allIds.Should().Contain(docId);
    }

    [Fact]
    public async Task AddDocument_ShouldStoreDocumentWithEmbedding()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        // Act
        var docId = await store.AddTextAsync(
            "User authentication and login functionality",
            new Dictionary<string, object> 
            { 
                ["category"] = "auth", 
                ["priority"] = "high",
                ["version"] = "1.0"
            }
        );

        // Assert
        docId.Should().NotBeNullOrEmpty();
        
        // Verify the document was actually stored by retrieving it
        var retrievedDoc = await store.GetAsync(docId);
        retrievedDoc.Should().NotBeNull();
        retrievedDoc!.Content.Should().Be("User authentication and login functionality");
        retrievedDoc.Embedding.Should().NotBeNull();
        retrievedDoc.Embedding.Should().HaveCount(768); // Nomic embedding dimension
        retrievedDoc.Metadata.Should().ContainKey("category");
        retrievedDoc.Metadata["category"].ToString().Should().Be("auth");
    }

    [Fact]
    public async Task SearchDocuments_ShouldReturnSimilarResults()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        var docIds = new[]
        {
            await store.AddTextAsync("User authentication and login functionality", new Dictionary<string, object> { ["category"] = "auth" }),
            await store.AddTextAsync("Database connection and data management", new Dictionary<string, object> { ["category"] = "database" }),
            await store.AddTextAsync("API endpoint security and validation", new Dictionary<string, object> { ["category"] = "security" })
        };

        // Act
        var results = await store.SearchTextAsync("login and security", limit: 2);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(2);
        results.Should().BeInDescendingOrder(r => r.Similarity);
        results[0].Similarity.Should().BeGreaterThan(0.5f); // Should have reasonable similarity
    }

    [Fact]
    public async Task SearchTextEnumerableAsync_ShouldReturnIEnumerableResults()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        var docIds = new[]
        {
            await store.AddTextAsync("User authentication and login functionality", new Dictionary<string, object> { ["category"] = "auth" }),
            await store.AddTextAsync("Database connection and data management", new Dictionary<string, object> { ["category"] = "database" }),
            await store.AddTextAsync("API endpoint security and validation", new Dictionary<string, object> { ["category"] = "security" })
        };

        // Act
        var results = await store.SearchTextEnumerableAsync("login and security", limit: 2);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(2);
        results.Should().BeInDescendingOrder(r => r.Similarity);
        results.First().Similarity.Should().BeGreaterThan(0.5f); // Should have reasonable similarity
        
        // Test LINQ operations work
        var authResults = results.Where(r => r.Document.Metadata.ContainsKey("category") && 
                                           r.Document.Metadata["category"].ToString() == "auth");
        authResults.Should().NotBeEmpty();
        
        var topResult = results.OrderByDescending(r => r.Similarity).First();
        topResult.Should().NotBeNull();
        topResult.Similarity.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetDocument_ShouldRetrieveStoredDocument()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        var docId = await store.AddTextAsync("Test document content", new Dictionary<string, object> { ["test"] = "value" });

        // Act
        var retrievedDoc = await store.GetAsync(docId);

        // Assert
        retrievedDoc.Should().NotBeNull();
        retrievedDoc!.Id.Should().Be(docId);
        retrievedDoc.Content.Should().Be("Test document content");
        retrievedDoc.Metadata.Should().ContainKey("test");
        retrievedDoc.Metadata["test"].ToString().Should().Be("value");
        retrievedDoc.Embedding.Should().NotBeNull();
        retrievedDoc.Embedding.Should().HaveCount(768); // Nomic embedding dimension
    }

    [Fact]
    public async Task DeleteDocument_ShouldRemoveDocument()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        var docId = await store.AddTextAsync("Document to delete", new Dictionary<string, object> { ["test"] = "value" });

        // Act
        var deleted = await store.DeleteAsync(docId);

        // Assert
        deleted.Should().BeTrue();
        var retrievedDoc = await store.GetAsync(docId);
        retrievedDoc.Should().BeNull();
    }

    [Fact]
    public async Task GetAllIds_ShouldReturnAllDocumentIds()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        var doc1Id = await store.AddTextAsync("Document 1", new Dictionary<string, object>());
        var doc2Id = await store.AddTextAsync("Document 2", new Dictionary<string, object>());
        var doc3Id = await store.AddTextAsync("Document 3", new Dictionary<string, object>());

        // Act
        var allIds = await store.GetAllIdsAsync();

        // Assert
        allIds.Should().HaveCount(3);
        allIds.Should().Contain(doc1Id);
        allIds.Should().Contain(doc2Id);
        allIds.Should().Contain(doc3Id);
    }

    [Fact]
    public async Task CreateStore_ShouldFailIfDirectoryExistsWithData()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);
        
        var docId = await store.AddTextAsync("Test document", new Dictionary<string, object>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            FileVectorStore.CreateAsync(_testStorePath));
        
        exception.Message.Should().Contain("already exists and contains vector store data");
    }

    [Fact]
    public async Task OpenStore_ShouldFailIfDirectoryDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentStore");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() => 
            FileVectorStore.OpenAsync(nonExistentPath));
        
        exception.Message.Should().Contain("does not exist");
    }

    [Fact]
    public async Task DeleteStore_ShouldRemoveEntireStore()
    {
        // Arrange
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);
        
        var docId = await store.AddTextAsync("Test document", new Dictionary<string, object>());

        // Act
        var deleted = await FileVectorStore.DeleteAsync(_testStorePath);

        // Assert
        deleted.Should().BeTrue();
        Directory.Exists(_testStorePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteStore_ShouldReturnFalseForNonExistentStore()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentStore");

        // Act
        var deleted = await FileVectorStore.DeleteAsync(nonExistentPath);

        // Assert
        deleted.Should().BeFalse();
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

    [Fact]
    public async Task CreateOrOpenAsync_ShouldCreateNewStore_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var storePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        
        // Act
        using var store = await FileVectorStore.CreateOrOpenAsync(storePath);
        
        // Assert
        store.Should().NotBeNull();
        Directory.Exists(storePath).Should().BeTrue();
        File.Exists(Path.Combine(storePath, "config.json")).Should().BeTrue();
        
        // Cleanup
        await FileVectorStore.DeleteAsync(storePath);
    }

    [Fact]
    public async Task CreateOrOpenAsync_ShouldOpenExistingStore_WhenValidStoreExists()
    {
        // Arrange
        var storePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var originalStore = await FileVectorStore.CreateAsync(storePath);
        var content = "This is a test document.";
        var docId = await originalStore.AddTextAsync(content);
        originalStore.Dispose();
        
        // Act
        using var reopenedStore = await FileVectorStore.CreateOrOpenAsync(storePath);
        
        // Assert
        reopenedStore.Should().NotBeNull();
        var retrievedDoc = await reopenedStore.GetAsync(docId);
        retrievedDoc.Should().NotBeNull();
        retrievedDoc!.Content.Should().Be(content);
        
        // Cleanup
        await FileVectorStore.DeleteAsync(storePath);
    }

    [Fact]
    public async Task CreateOrOpenAsync_ShouldCreateNewStore_WhenDirectoryExistsButIsNotValidStore()
    {
        // Arrange
        var storePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(storePath);
        // Create a file that's not a valid store
        File.WriteAllText(Path.Combine(storePath, "somefile.txt"), "not a vector store");
        
        // Act
        using var store = await FileVectorStore.CreateOrOpenAsync(storePath);
        
        // Assert
        store.Should().NotBeNull();
        File.Exists(Path.Combine(storePath, "config.json")).Should().BeTrue();
        File.Exists(Path.Combine(storePath, "somefile.txt")).Should().BeTrue(); // Original file should still be there
        
        // Cleanup
        await FileVectorStore.DeleteAsync(storePath);
    }

    [Fact]
    public async Task CreateOrOpenAsync_ShouldUseProvidedOptions_WhenCreatingNewStore()
    {
        // Arrange
        var storePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var customOptions = new VectorStoreOptions
        {
            StorePath = storePath,
            ChunkSize = 1000,
            MaxMemoryChunks = 10,
            EnableEmbeddingGeneration = true,
            UseMemoryMapping = false
        };
        
        // Act
        using var store = await FileVectorStore.CreateOrOpenAsync(storePath, customOptions);
        
        // Assert
        store.Should().NotBeNull();
        // Note: We can't directly access the options from the store, but we can verify it was created
        Directory.Exists(storePath).Should().BeTrue();
        
        // Cleanup
        await FileVectorStore.DeleteAsync(storePath);
    }
}
