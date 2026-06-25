namespace LexAI.Api.Services;

public interface IEmbeddingService
{
    Task<ReadOnlyMemory<float>> EmbedQueryAsync(string query, CancellationToken ct = default);
    Task<ReadOnlyMemory<float>> EmbedPassageAsync(string text, CancellationToken ct = default);
}
