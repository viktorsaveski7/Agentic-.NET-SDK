using Microsoft.Extensions.AI;

namespace LexAI.Api.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly bool _usePrefixes;

    public OllamaEmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IConfiguration configuration)
    {
        _generator = generator;
        _usePrefixes = configuration.GetValue<bool>("Ollama:UsePrefixes");
    }

    public Task<ReadOnlyMemory<float>> EmbedQueryAsync(string query, CancellationToken ct = default)
    {
        var input = _usePrefixes ? "query: " + query : query;
        return _generator.GenerateVectorAsync(input, cancellationToken: ct);
    }

    public Task<ReadOnlyMemory<float>> EmbedPassageAsync(string text, CancellationToken ct = default)
    {
        var input = _usePrefixes ? "passage: " + text : text;
        return _generator.GenerateVectorAsync(input, cancellationToken: ct);
    }
}
