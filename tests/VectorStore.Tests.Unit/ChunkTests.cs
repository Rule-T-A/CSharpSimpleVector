using FluentAssertions;
using VectorStore.Storage;
using VectorStore.Models;
using VectorStore.Core;
using Xunit;

namespace VectorStore.Tests.Unit;

/// <summary>
/// Unit tests for storage functionality.
/// These tests validate data storage and indexing operations.
/// </summary>
public class ChunkTests
{
    [Fact]
    public void EmbeddingChunk_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var chunk = new EmbeddingChunk();

        // Assert
        chunk.Should().NotBeNull();
        chunk.ChunkId.Should().Be(0);
        chunk.Count.Should().Be(0);
        chunk.Dimensions.Should().Be(0);
        chunk.Embeddings.Should().NotBeNull();
        chunk.Embeddings.Should().BeEmpty();
    }

    [Fact]
    public void EmbeddingChunk_ShouldAllowPropertySetters()
    {
        // Arrange
        var chunk = new EmbeddingChunk();
        var embeddings = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        chunk.ChunkId = 1;
        chunk.Count = 5;
        chunk.Dimensions = 3;
        chunk.Embeddings = embeddings;
        chunk.CreatedAt = DateTime.UtcNow;

        // Assert
        chunk.ChunkId.Should().Be(1);
        chunk.Count.Should().Be(5);
        chunk.Dimensions.Should().Be(3);
        chunk.Embeddings.Should().BeEquivalentTo(embeddings);
        chunk.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MetadataChunk_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var chunk = new MetadataChunk();

        // Assert
        chunk.Should().NotBeNull();
        chunk.ChunkId.Should().Be(0);
        chunk.Count.Should().Be(0);
        chunk.Documents.Should().NotBeNull();
        chunk.Documents.Should().BeEmpty();
    }

    [Fact]
    public void MetadataChunk_ShouldAllowPropertySetters()
    {
        // Arrange
        var chunk = new MetadataChunk();
        var documents = new List<VectorDocument>
        {
            new VectorDocument { Id = "1", Content = "Test 1", Metadata = new Dictionary<string, object>() },
            new VectorDocument { Id = "2", Content = "Test 2", Metadata = new Dictionary<string, object>() }
        };

        // Act
        chunk.ChunkId = 1;
        chunk.Count = 2;
        chunk.Documents = documents;
        chunk.CreatedAt = DateTime.UtcNow;

        // Assert
        chunk.ChunkId.Should().Be(1);
        chunk.Count.Should().Be(2);
        chunk.Documents.Should().BeEquivalentTo(documents);
        chunk.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ChunkManager_ShouldRequireOptions()
    {
        // Arrange
        var options = new VectorStoreOptions();

        // Act
        var manager = new ChunkManager(options);

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact]
    public void ChunkManager_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new ChunkManager(null!);
        action.Should().Throw<ArgumentNullException>("Null options should throw exception");
    }
}
