# VectorStore

A high-performance, file-based vector storage and similarity search library for .NET with local Nomic embedding generation and intelligent document processing.

## Features

- **File-Based Storage**: No database dependencies
- **Local Embeddings**: Nomic embedding generation with HuggingFace model caching
- **Document Processing**: Smart chunking for text, Markdown, PDF, and DOCX files
- **High Performance**: Memory-mapped files and SIMD optimization
- **Flexible Search**: Cosine similarity with metadata filtering
- **LINQ Support**: IEnumerable-based search results for easy filtering
- **Developer Friendly**: Simple async/await API with static factory methods and "just works" experience
- **Robust Storage**: File corruption recovery and graceful error handling

## Quick Start

### The "Just Works" Experience

For development and quick prototyping, use `CreateOrOpenAsync` - it automatically creates a new store if the directory doesn't exist or isn't a valid vector store, or opens an existing one if it is:

```csharp
// This will work whether the directory exists or not!
using var store = await FileVectorStore.CreateOrOpenAsync("./my-vectors");

// Add some content
await store.AddTextAsync("Hello, world!");

// Search
var results = await store.SearchTextAsync("world");
```

### Creating and Opening Stores

```csharp
// Create a new store
using var store = await FileVectorStore.CreateAsync("./my-vectors");

// Open an existing store
using var existingStore = await FileVectorStore.OpenAsync("./my-vectors");

// Create or open - "just works" for development
using var devStore = await FileVectorStore.CreateOrOpenAsync("./my-vectors");

// Delete a store
await FileVectorStore.DeleteAsync("./my-vectors");
```

### Adding Documents

```csharp
// Simple text addition (embeddings generated automatically)
var docId = await store.AddTextAsync("LoginController calls UserService", new Dictionary<string, object>
{
    ["caller"] = "LoginController",
    ["callee"] = "UserService",
    ["project"] = "MyApp.Web"
});

// Batch addition
var docIds = new[]
{
    await store.AddTextAsync("User authentication and login functionality", new Dictionary<string, object> { ["category"] = "auth" }),
    await store.AddTextAsync("Database connection and data management", new Dictionary<string, object> { ["category"] = "database" }),
    await store.AddTextAsync("API endpoint security and validation", new Dictionary<string, object> { ["category"] = "security" })
};
```

### Document Processing

```csharp
// Add individual documents with smart chunking
var documentIds = await store.AddDocumentAsync("./documents/manual.pdf");
var markdownIds = await store.AddDocumentAsync("./docs/README.md");

// Add documents with custom chunking options
var customOptions = new SmartChunkingOptions
{
    MaxChunkSize = 1000,
    MinChunkSize = 200,
    OverlapSize = 100,
    Strategy = SmartChunkingStrategy.Hybrid
};
var customIds = await store.AddDocumentAsync("./documents/large-doc.docx", customOptions);

// Add all supported documents from a directory
var allDocumentIds = await store.AddDocumentsAsync("./documents/");

// Parse documents without adding to store (for analysis)
var parseResult = await store.ParseDocumentAsync("./documents/report.pdf");
Console.WriteLine($"Document has {parseResult.TotalChunks} chunks");
Console.WriteLine($"Title: {parseResult.Title}");
Console.WriteLine($"Word count: {parseResult.TotalWordCount}");
```

### Supported Document Types

- **Text Files**: `.txt`, `.log`, `.csv`, `.json`, `.xml`
- **Markdown**: `.md`, `.markdown` (with header detection and code block awareness)
- **PDF**: `.pdf` (page-by-page processing with metadata extraction)
- **Word Documents**: `.docx` (paragraph-based chunking with style detection)

### Searching Documents

```csharp
// Array-based search results
var results = await store.SearchTextAsync("authentication methods", limit: 5);

foreach (var result in results)
{
    Console.WriteLine($"Similarity: {result.Similarity:F3}");
    Console.WriteLine($"Content: {result.Document.Content}");
    Console.WriteLine($"Category: {result.Document.Metadata["category"]}");
}

// IEnumerable-based search results (LINQ-friendly)
var searchResults = await store.SearchTextEnumerableAsync("login and security", limit: 10);

// Use LINQ operations
var authResults = searchResults
    .Where(r => r.Document.Metadata.ContainsKey("category") && 
                r.Document.Metadata["category"].ToString() == "auth")
    .OrderByDescending(r => r.Similarity)
    .Take(3);

var highSimilarityResults = searchResults
    .Where(r => r.Similarity > 0.8f)
    .Select(r => r.Document.Content);
```

### Advanced Operations

```csharp
// Document management
var doc = await store.GetAsync(docId);
var allIds = await store.GetAllIdsAsync();
var deleted = await store.DeleteAsync(docId);

// Manual embedding control
var embedding = await store.GenerateEmbeddingAsync("some text");
var embeddings = await store.GenerateEmbeddingsAsync(new[] { "text1", "text2" });

// Create documents with custom embeddings
var customDoc = new VectorDocument
{
    Content = "Your content here",
    Embedding = embedding,
    Metadata = new Dictionary<string, object> { ["source"] = "custom" }
};
await store.AddAsync(customDoc);

// Progress reporting for model downloads
await store.AddTextAsync("text", metadata: null, progressCallback: (bytesDownloaded, totalBytes, percentage) =>
{
    Console.WriteLine($"Download progress: {percentage:F1}% ({bytesDownloaded:N0}/{totalBytes:N0} bytes)");
});
```

### Configuration Options

```csharp
var options = new VectorStoreOptions
{
    StorePath = "./my-vectors",
    ChunkSize = 1000,
    EnableEmbeddingGeneration = true,        // Enable Nomic embedding generation
    EmbeddingCacheSize = 1000,               // Max items in memory cache
    EmbeddingModelPath = ""                  // Custom model path (empty = auto-download)
};

using var store = await FileVectorStore.CreateAsync("./my-vectors", options);
```

### Smart Chunking Options

```csharp
var chunkingOptions = new SmartChunkingOptions
{
    MaxChunkSize = 500,                      // Maximum characters per chunk
    MinChunkSize = 100,                      // Minimum characters per chunk
    OverlapSize = 50,                        // Characters to overlap between chunks
    Strategy = SmartChunkingStrategy.Hybrid  // Chunking strategy
};

// Available strategies:
// - Semantic: Prioritizes sentence and paragraph boundaries
// - Structural: Prioritizes headers, code blocks, and structural elements
// - Hybrid: Combines both approaches for optimal results
```

## API Reference

### Core Methods

| Method | Description | Returns |
|--------|-------------|---------|
| `CreateAsync(path, options?)` | Create a new vector store | `Task<FileVectorStore>` |
| `OpenAsync(path, options?)` | Open an existing vector store | `Task<FileVectorStore>` |
| `CreateOrOpenAsync(path, options?)` | Create new or open existing store (just works!) | `Task<FileVectorStore>` |
| `DeleteAsync(path)` | Delete an entire vector store | `Task<bool>` |
| `AddTextAsync(content, metadata?)` | Add text with auto-generated embedding | `Task<string>` (document ID) |
| `SearchTextAsync(query, limit?)` | Search by text (array results) | `Task<SearchResult[]>` |
| `SearchTextEnumerableAsync(query, limit?)` | Search by text (LINQ-friendly) | `Task<IEnumerable<SearchResult>>` |
| `GetAsync(id)` | Retrieve document by ID | `Task<VectorDocument?>` |
| `DeleteAsync(id)` | Delete document by ID | `Task<bool>` |
| `GetAllIdsAsync()` | Get all document IDs | `Task<string[]>` |

### Document Processing Methods

| Method | Description | Returns |
|--------|-------------|---------|
| `ParseDocumentAsync(filePath, options?)` | Parse document without adding to store | `Task<DocumentParseResult>` |
| `AddDocumentAsync(filePath, options?)` | Add single document with smart chunking | `Task<string[]>` (chunk IDs) |
| `AddDocumentsAsync(directoryPath, options?)` | Add all supported documents from directory | `Task<string[]>` (all chunk IDs) |

### Embedding Methods

| Method | Description | Returns |
|--------|-------------|---------|
| `GenerateEmbeddingAsync(text, progress?)` | Generate single embedding | `Task<float[]>` |
| `GenerateEmbeddingsAsync(texts, progress?)` | Generate multiple embeddings | `Task<float[][]>` |

## Performance

- **Local Inference**: No API calls after initial model download
- **Caching**: File-based and in-memory caching for fast repeated queries
- **SIMD Optimization**: Vectorized similarity calculations
- **Memory Efficient**: Lazy loading and binary index persistence
- **Smart Chunking**: Intelligent document splitting at semantic boundaries
- **Batch Processing**: Efficient handling of multiple documents and chunks

## Reliability

- **File Corruption Recovery**: Automatically detects and recovers from corrupted index files
- **Graceful Degradation**: Falls back to JSON loading if binary index is corrupted
- **Data Validation**: Skips corrupted document files during loading
- **Self-Healing**: Automatically rebuilds indexes from valid data when needed

## Project Structure

- `src/VectorStore/` - Main library
- `tests/` - Unit, integration, and benchmark tests
  - `VectorStore.Tests.Unit/` - Unit tests for individual components
  - `VectorStore.Tests.Integration/` - End-to-end integration tests
  - `VectorStore.Tests.Benchmarks/` - Performance benchmarks

## Building

```bash
dotnet build
```

## Testing

The project includes comprehensive test coverage with 38 tests across unit, integration, and benchmark categories:

```bash
# Run all tests (38 tests total)
dotnet test

# Run specific test projects
dotnet test tests/VectorStore.Tests.Unit/        # 21 unit tests
dotnet test tests/VectorStore.Tests.Integration/ # 17 integration tests
dotnet test tests/VectorStore.Tests.Benchmarks/  # Performance benchmarks
```

## Requirements

- .NET 8.0 or later
- Windows, Linux, or macOS
- Internet connection for initial model download (subsequent usage is offline)

## License

This project is dual-licensed:

- **MIT License**: Free for open source and personal use
- **Commercial License**: Required for commercial/proprietary use

See [LICENSE](LICENSE) for MIT terms and [COMMERCIAL_LICENSE](COMMERCIAL_LICENSE) for commercial terms.

For commercial licensing inquiries, contact [your-email@example.com](mailto:your-email@example.com).

### Third-Party Licenses

This project uses the following third-party libraries, all with permissive licenses compatible with both MIT and commercial use:

- Microsoft.ML.OnnxRuntime (MIT)
- HuggingFace.NET (MIT)
- Markdig (BSD-2-Clause)
- PdfPig (MIT)
- DocumentFormat.OpenXml (MIT)
- Microsoft.Extensions.* (MIT)
- System.* (MIT)
