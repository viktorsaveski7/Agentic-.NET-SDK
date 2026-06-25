using System.Text;
using System.Text.Json;
using LexAI.Api.Models;
using LexAI.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LexAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IEmbeddingService _embedding;
    private readonly IVectorStoreService _vectorStore;
    private readonly ILlmService _llm;
    private readonly IIngestionService _ingestion;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IEmbeddingService embedding,
        IVectorStoreService vectorStore,
        ILlmService llm,
        IIngestionService ingestion,
        ILogger<ChatController> logger)
    {
        _embedding = embedding;
        _vectorStore = vectorStore;
        _llm = llm;
        _ingestion = ingestion;
        _logger = logger;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "Question is required." });

        var ct = HttpContext.RequestAborted;
        var results = await RetrieveAsync(request.Question, 10, ct);

        var sb = new StringBuilder();
        await foreach (var token in _llm.StreamAsync(request.Question, results, request.ConversationHistory, ct))
            sb.Append(token);

        var answer = sb.ToString();
        var followUps = await _llm.GenerateFollowUpsAsync(request.Question, answer, ct);

        return Ok(new ChatResponse
        {
            Answer = answer,
            Sources = ToSources(results),
            Confidence = Confidence(results),
            FollowUps = followUps
        });
    }

    [HttpPost("ask/stream")]
    public async Task AskStream([FromBody] ChatRequest request)
    {
        var ct = HttpContext.RequestAborted;

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            await WriteEvent(new { type = "error", message = "Question is required." }, ct);
            return;
        }

        List<SearchResult> results;
        try
        {
            results = await RetrieveAsync(request.Question, 10, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retrieval failed");
            await WriteEvent(new { type = "error", message = ex.Message }, ct);
            return;
        }

        await WriteEvent(new { type = "sources", sources = ToSources(results), confidence = Confidence(results) }, ct);

        var sb = new StringBuilder();
        try
        {
            await foreach (var token in _llm.StreamAsync(request.Question, results, request.ConversationHistory, ct))
            {
                sb.Append(token);
                await WriteEvent(new { type = "token", content = token }, ct);
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming failed");
            await WriteEvent(new { type = "error", message = ex.Message }, ct);
            return;
        }

        var followUps = await _llm.GenerateFollowUpsAsync(request.Question, sb.ToString(), ct);
        await WriteEvent(new { type = "followups", followUps }, ct);
        await WriteEvent(new { type = "done" }, ct);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query is required." });

        var ct = HttpContext.RequestAborted;
        var topK = request.TopK <= 0 ? 5 : request.TopK;
        var results = await RetrieveAsync(request.Query, topK, ct);
        return Ok(ToSources(results));
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromQuery] bool scrape = true)
    {
        var ct = HttpContext.RequestAborted;
        var result = await _ingestion.IngestAsync(scrape, progress: null, ct);
        return Ok(result);
    }

    [HttpGet("documents")]
    public async Task<IActionResult> Documents()
    {
        var ct = HttpContext.RequestAborted;
        var chunks = await _vectorStore.CountAsync(ct);
        var lawNames = await _vectorStore.ListLawNamesAsync(ct);
        return Ok(new { laws = lawNames.Count, chunks, lawNames });
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });

    private async Task<List<SearchResult>> RetrieveAsync(string query, int topK, CancellationToken ct)
    {
        var queryVector = await _embedding.EmbedQueryAsync(query, ct);
        return await _vectorStore.SearchAsync(queryVector, topK, ct);
    }

    private static List<Source> ToSources(IEnumerable<SearchResult> results) =>
        results.Select(r => new Source
        {
            LawName = r.LawName,
            Page = r.Page,
            Chunk = r.Document,
            Score = Math.Round(r.Score, 4)
        }).ToList();

    private static double Confidence(IReadOnlyList<SearchResult> results) =>
        results.Count == 0 ? 0 : Math.Round(Math.Clamp(results.Max(r => r.Score), 0, 1), 4);

    private async Task WriteEvent(object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
