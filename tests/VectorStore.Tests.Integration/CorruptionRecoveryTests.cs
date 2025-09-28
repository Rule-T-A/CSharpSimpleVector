using FluentAssertions;
using VectorStore.Core;
using VectorStore.Models;
using Xunit;

namespace VectorStore.Tests.Integration;

public class CorruptionRecoveryTests : IDisposable
{
    private readonly string _testStorePath;
    private readonly List<string> _createdStores = new();

    public CorruptionRecoveryTests()
    {
        _testStorePath = Path.Combine(Path.GetTempPath(), $"corruption_test_{Guid.NewGuid()}");
    }

    [Fact]
    public async Task Should_Recover_From_Interrupted_Write()
    {
        // Arrange - Create a store and add some documents
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        // Add initial documents
        await store.AddTextAsync("Initial document 1");
        await store.AddTextAsync("Initial document 2");

        // Simulate: process killed during document addition
        // Create a partial file that would be left behind
        var partialDocPath = Path.Combine(_testStorePath, "documents", $"partial_{Guid.NewGuid()}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(partialDocPath)!);
        
        // Write partial JSON (incomplete document)
        var partialContent = """{"id":"partial_doc","content":"This is a partial document that was interrupted","metadata":{""";
        await File.WriteAllTextAsync(partialDocPath, partialContent);

        // Act - Open the store again (should handle partial files gracefully)
        using var recoveredStore = await FileVectorStore.OpenAsync(_testStorePath);

        // Assert - Should be able to read existing documents
        var allIds = await recoveredStore.GetAllIdsAsync();
        allIds.Should().HaveCount(2); // Only the complete documents
        allIds.Should().NotContain("partial_doc"); // Partial document should be ignored

        // Should be able to add new documents
        var newDocId = await recoveredStore.AddTextAsync("New document after recovery");
        newDocId.Should().NotBeNullOrEmpty();

        // Should be able to search (will include the new document)
        var searchResults = await recoveredStore.SearchTextAsync("Initial");
        searchResults.Should().HaveCountGreaterThan(0); // At least the original documents
    }

    [Fact]
    public async Task Should_Handle_Corrupted_Index_Files()
    {
        // Arrange - Create a store with some data
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        await store.AddTextAsync("Test document 1");
        await store.AddTextAsync("Test document 2");

        // Corrupt the index file
        var indexFile = Path.Combine(_testStorePath, "vector_index.bin");
        await File.WriteAllTextAsync(indexFile, "corrupted data");

        // Act - Should rebuild index automatically
        using var recoveredStore = await FileVectorStore.OpenAsync(_testStorePath);

        // Assert - Should be able to search (index should be rebuilt)
        var searchResults = await recoveredStore.SearchTextAsync("Test");
        searchResults.Should().HaveCount(2);

        // Should be able to add new documents
        var newDocId = await recoveredStore.AddTextAsync("New document after index rebuild");
        newDocId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Handle_Missing_Index_File()
    {
        // Arrange - Create a store with some data
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        await store.AddTextAsync("Test document 1");
        await store.AddTextAsync("Test document 2");

        // Delete the index file
        var indexFile = Path.Combine(_testStorePath, "vector_index.bin");
        File.Delete(indexFile);

        // Act - Should rebuild index automatically
        using var recoveredStore = await FileVectorStore.OpenAsync(_testStorePath);

        // Assert - Should be able to search (index should be rebuilt)
        var searchResults = await recoveredStore.SearchTextAsync("Test");
        searchResults.Should().HaveCount(2);

        // Index file should be recreated
        File.Exists(indexFile).Should().BeTrue();
    }

    [Fact]
    public async Task Should_Handle_Corrupted_Document_Files()
    {
        // Arrange - Create a store with some data
        using var store = await FileVectorStore.CreateAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        var docId1 = await store.AddTextAsync("Valid document 1");
        var docId2 = await store.AddTextAsync("Valid document 2");

        // Corrupt one of the document files by writing invalid JSON
        var docPath = Path.Combine(_testStorePath, "documents", $"{docId1}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(docPath)!);
        await File.WriteAllTextAsync(docPath, "this is not valid json at all");

        // Also corrupt the binary index to force reload from JSON files
        var indexPath = Path.Combine(_testStorePath, "vector_index.bin");
        await File.WriteAllTextAsync(indexPath, "corrupted binary index");

        // Act - Should handle corrupted documents gracefully
        using var recoveredStore = await FileVectorStore.OpenAsync(_testStorePath);

        // Assert - Should be able to read valid documents
        var allIds = await recoveredStore.GetAllIdsAsync();
        allIds.Should().HaveCount(1); // Only the valid document
        allIds.Should().Contain(docId2);
        allIds.Should().NotContain(docId1); // Corrupted document should be ignored

        // Should be able to search
        var searchResults = await recoveredStore.SearchTextAsync("Valid");
        searchResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_Handle_Empty_Store_Directory()
    {
        // Arrange - Create an empty directory
        Directory.CreateDirectory(_testStorePath);

        // Act - Should create a new store
        using var store = await FileVectorStore.OpenAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        // Assert - Should be able to add documents
        var docId = await store.AddTextAsync("First document in empty store");
        docId.Should().NotBeNullOrEmpty();

        // Should be able to search
        var searchResults = await store.SearchTextAsync("First");
        searchResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_Handle_Store_With_Only_Config_File()
    {
        // Arrange - Create a store directory with only config
        Directory.CreateDirectory(_testStorePath);
        var configPath = Path.Combine(_testStorePath, "config.json");
        var config = """{"StorePath":"test","ChunkSize":1000,"MaxMemoryChunks":100,"EnableEmbeddingGeneration":true,"UseMemoryMapping":true}""";
        await File.WriteAllTextAsync(configPath, config);

        // Act - Should open successfully
        using var store = await FileVectorStore.OpenAsync(_testStorePath);
        _createdStores.Add(_testStorePath);

        // Assert - Should be able to add documents
        var docId = await store.AddTextAsync("Document in config-only store");
        docId.Should().NotBeNullOrEmpty();
    }

    public void Dispose()
    {
        // Clean up test stores
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
