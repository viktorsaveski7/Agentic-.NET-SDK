using LexAI.Api.Models;

namespace LexAI.Api.Services;

public interface IVectorStoreService
{
    Task UpsertAsync(
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> embeddings,
        CancellationToken ct = default);

    Task<List<SearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int nResults = 5,
        CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

    Task<List<string>> ListLawNamesAsync(CancellationToken ct = default);
}
