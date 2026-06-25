using System.Text.Json;
using ChromaDB.Client;
using LexAI.Api.Models;

namespace LexAI.Api.Services;

public class ChromaVectorStoreService : IVectorStoreService, IDisposable
{
    private readonly ChromaConfigurationOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ChromaClient _client;
    private readonly string _collectionName;
    private readonly ILogger<ChromaVectorStoreService> _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private ChromaCollectionClient? _collectionClient;

    public ChromaVectorStoreService(IConfiguration configuration, ILogger<ChromaVectorStoreService> logger)
    {
        _logger = logger;
        var baseUrl = configuration["ChromaDB:BaseUrl"] ?? "http://localhost:8001";
        _collectionName = configuration["ChromaDB:CollectionName"] ?? "macedonian_laws";

        _options = new ChromaConfigurationOptions(uri: baseUrl);
        _httpClient = new HttpClient();
        _client = new ChromaClient(_options, _httpClient);
    }

    private async Task<ChromaCollectionClient> GetCollectionAsync()
    {
        if (_collectionClient is not null) return _collectionClient;

        await _initLock.WaitAsync();
        try
        {
            if (_collectionClient is null)
            {
                var collection = await _client.GetOrCreateCollection(_collectionName);
                _collectionClient = new ChromaCollectionClient(collection, _options, _httpClient);
                _logger.LogInformation("Connected to ChromaDB collection '{Collection}'", _collectionName);
            }
            return _collectionClient;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task UpsertAsync(
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> embeddings,
        CancellationToken ct = default)
    {
        if (chunks.Count == 0) return;

        var collection = await GetCollectionAsync();

        var ids = chunks.Select(c => c.Id).ToList();
        var vectors = embeddings.ToList();
        var documents = chunks.Select(c => c.Text).ToList();
        var metadatas = chunks.Select(c => new Dictionary<string, object>
        {
            ["law"] = c.LawName,
            ["page"] = c.Page,
            ["chunk"] = c.ChunkIndex
        }).ToList();

        await collection.Upsert(ids, vectors, metadatas, documents);
    }

    public async Task<List<SearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int nResults = 5,
        CancellationToken ct = default)
    {
        var collection = await GetCollectionAsync();

        var entries = await collection.Query(
            queryEmbedding,
            nResults,
            include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Documents | ChromaQueryInclude.Distances);

        return entries.Select(e => new SearchResult
        {
            Id = e.Id,
            Document = e.Document ?? string.Empty,
            Distance = e.Distance,
            LawName = MetaString(e.Metadata, "law"),
            Page = MetaInt(e.Metadata, "page"),
            ChunkIndex = MetaInt(e.Metadata, "chunk")
        }).ToList();
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var collection = await GetCollectionAsync();
        return await collection.Count();
    }

    public async Task<List<string>> ListLawNamesAsync(CancellationToken ct = default)
    {
        var collection = await GetCollectionAsync();
        var entries = await collection.Get(
            ids: null!,
            where: null!,
            whereDocument: null!,
            limit: 100000,
            offset: 0,
            include: ChromaGetInclude.Metadatas);

        return entries
            .Select(e => MetaString(e.Metadata, "law"))
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .OrderBy(s => s)
            .ToList();
    }

    private static string MetaString(Dictionary<string, object>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var v) || v is null) return string.Empty;
        if (v is JsonElement je)
            return je.ValueKind == JsonValueKind.String ? je.GetString() ?? string.Empty : je.ToString();
        return v.ToString() ?? string.Empty;
    }

    private static int MetaInt(Dictionary<string, object>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var v) || v is null) return 0;
        if (v is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var n)) return n;
            return int.TryParse(je.ToString(), out var p) ? p : 0;
        }
        try { return Convert.ToInt32(v); }
        catch { return int.TryParse(v.ToString(), out var p) ? p : 0; }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _initLock.Dispose();
    }
}
