using FluentAssertions;
using VectorStore.Storage;
using VectorStore.Core;
using Xunit;

namespace VectorStore.Tests.Unit;

/// <summary>
/// Unit tests for index management functionality.
/// These tests validate index building and querying operations.
/// </summary>
public class IndexTests
{
    [Fact]
    public void VectorIndex_ShouldInitializeCorrectly()
    {
        // Arrange
        var storePath = "/test/store";

        // Act
        var index = new VectorIndex(storePath);

        // Assert
        index.Should().NotBeNull();
        index.Count.Should().Be(0);
    }

    [Fact]
    public void VectorIndex_ShouldAddVectors()
    {
        // Arrange
        var index = new VectorIndex("/test/store");
        var id = "doc-1";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var filePath = "/test/store/doc-1.json";

        // Act
        index.AddVector(id, embedding, filePath);

        // Assert
        index.Count.Should().Be(1);
        var vector = index.GetVector(id);
        vector.Should().NotBeNull();
        vector!.Id.Should().Be(id);
        vector.Embedding.Should().BeEquivalentTo(embedding);
        vector.FilePath.Should().Be(filePath);
    }

    [Fact]
    public void VectorIndex_ShouldRemoveVectors()
    {
        // Arrange
        var index = new VectorIndex("/test/store");
        var id = "doc-1";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var filePath = "/test/store/doc-1.json";
        index.AddVector(id, embedding, filePath);

        // Act
        var removed = index.RemoveVector(id);

        // Assert
        removed.Should().BeTrue();
        index.Count.Should().Be(0);
        index.GetVector(id).Should().BeNull();
    }

    [Fact]
    public void VectorIndex_ShouldReturnAllVectors()
    {
        // Arrange
        var index = new VectorIndex("/test/store");
        index.AddVector("doc-1", new float[] { 0.1f, 0.2f, 0.3f }, "/test/store/doc-1.json");
        index.AddVector("doc-2", new float[] { 0.4f, 0.5f, 0.6f }, "/test/store/doc-2.json");

        // Act
        var vectors = index.GetAllVectors();

        // Assert
        vectors.Should().HaveCount(2);
        vectors.Should().Contain(v => v.Id == "doc-1");
        vectors.Should().Contain(v => v.Id == "doc-2");
    }

    [Fact]
    public void VectorIndex_ShouldClearAllVectors()
    {
        // Arrange
        var index = new VectorIndex("/test/store");
        index.AddVector("doc-1", new float[] { 0.1f, 0.2f, 0.3f }, "/test/store/doc-1.json");
        index.AddVector("doc-2", new float[] { 0.4f, 0.5f, 0.6f }, "/test/store/doc-2.json");

        // Act
        index.Clear();

        // Assert
        index.Count.Should().Be(0);
        index.GetAllVectors().Should().BeEmpty();
    }

    [Fact]
    public void VectorIndex_ShouldHandleNonExistentVector()
    {
        // Arrange
        var index = new VectorIndex("/test/store");

        // Act
        var vector = index.GetVector("non-existent");

        // Assert
        vector.Should().BeNull();
    }

    [Fact]
    public void VectorIndex_ShouldHandleRemovingNonExistentVector()
    {
        // Arrange
        var index = new VectorIndex("/test/store");

        // Act
        var removed = index.RemoveVector("non-existent");

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void IndexManager_ShouldRequireOptions()
    {
        // Arrange
        var options = new VectorStoreOptions();

        // Act
        var manager = new IndexManager(options);

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact]
    public void IndexManager_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new IndexManager(null!);
        action.Should().Throw<ArgumentNullException>("Null options should throw exception");
    }
}
