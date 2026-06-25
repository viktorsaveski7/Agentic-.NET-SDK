using LexAI.Api.Models;
using LexAI.Api.Scripts;

namespace LexAI.Api.Services;

public record IngestionResult(int Laws, int Chunks, List<string> LawNames, List<string> Log);

public interface IIngestionService
{
    Task<IngestionResult> IngestAsync(bool scrapeFirst, Action<string>? progress = null, CancellationToken ct = default);
}

public class IngestionService : IIngestionService
{
    private readonly LawScraper _scraper;
    private readonly IPdfProcessorService _pdf;
    private readonly IChunkingService _chunking;
    private readonly IEmbeddingService _embedding;
    private readonly IVectorStoreService _vectorStore;
    private readonly IConfiguration _config;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        LawScraper scraper,
        IPdfProcessorService pdf,
        IChunkingService chunking,
        IEmbeddingService embedding,
        IVectorStoreService vectorStore,
        IConfiguration config,
        ILogger<IngestionService> logger)
    {
        _scraper = scraper;
        _pdf = pdf;
        _chunking = chunking;
        _embedding = embedding;
        _vectorStore = vectorStore;
        _config = config;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAsync(bool scrapeFirst, Action<string>? progress = null, CancellationToken ct = default)
    {
        var log = new List<string>();
        void Report(string msg)
        {
            _logger.LogInformation("{Message}", msg);
            log.Add(msg);
            progress?.Invoke(msg);
        }

        var pdfDir = _config["Ingestion:PdfDirectory"] ?? "./data/pdfs";
        var chunkSize = _config.GetValue("Ingestion:ChunkSize", 1000);
        var overlap = _config.GetValue("Ingestion:ChunkOverlap", 200);

        if (scrapeFirst)
        {
            Report("Scraping laws...");
            await _scraper.ScrapeAsync(Report, ct);
        }

        Directory.CreateDirectory(pdfDir);
        var pdfFiles = Directory.EnumerateFiles(pdfDir, "*.pdf", SearchOption.TopDirectoryOnly).ToList();
        Report($"Found {pdfFiles.Count} PDF files in {pdfDir}");

        var lawNames = new List<string>();
        int totalChunks = 0;

        foreach (var file in pdfFiles)
        {
            ct.ThrowIfCancellationRequested();
            var lawName = Path.GetFileNameWithoutExtension(file);
            Report($"Processing {lawName}...");

            List<ExtractedPage> pages;
            try
            {
                pages = _pdf.ExtractPages(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read {File}", file);
                Report($"Skipped {lawName}: {ex.Message}");
                continue;
            }

            var chunks = new List<DocumentChunk>();
            var embeddings = new List<ReadOnlyMemory<float>>();

            foreach (var page in pages)
            {
                var pieces = _chunking.ChunkText(page.Text, chunkSize, overlap);
                for (int i = 0; i < pieces.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var chunk = new DocumentChunk
                    {
                        LawName = lawName,
                        Page = page.Page,
                        ChunkIndex = i,
                        Text = pieces[i]
                    };
                    try
                    {
                        var vector = await _embedding.EmbedPassageAsync(pieces[i], ct);
                        chunks.Add(chunk);
                        embeddings.Add(vector);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Embedding failed for {Law} p{Page} chunk {Chunk}; skipping",
                            lawName, page.Page, i);
                    }
                }
            }

            if (chunks.Count > 0)
            {
                await _vectorStore.UpsertAsync(chunks, embeddings, ct);
                lawNames.Add(lawName);
                totalChunks += chunks.Count;
                Report($"Indexed {lawName}: {chunks.Count} chunks");
            }
            else
            {
                Report($"No usable text in {lawName}");
            }
        }

        Report($"Done. {lawNames.Count} laws, {totalChunks} chunks indexed.");
        return new IngestionResult(lawNames.Count, totalChunks, lawNames, log);
    }
}
