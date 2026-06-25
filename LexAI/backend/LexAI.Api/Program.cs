using LexAI.Api.Scripts;
using LexAI.Api.Services;
using Microsoft.Extensions.AI;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

var urls = builder.Configuration["Urls"];
if (!string.IsNullOrWhiteSpace(urls))
    builder.WebHost.UseUrls(urls.Split(';', StringSplitOptions.RemoveEmptyEntries));

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["Ollama:BaseUrl"] ?? "http://localhost:11434";
    var model = cfg["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
    return new OllamaApiClient(new Uri(baseUrl), model);
});

builder.Services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddSingleton<IVectorStoreService, ChromaVectorStoreService>();
builder.Services.AddSingleton<IPdfProcessorService, PdfProcessorService>();
builder.Services.AddSingleton<IChunkingService, ChunkingService>();
builder.Services.AddSingleton<ILlmService, LlmService>();
builder.Services.AddSingleton<LawScraper>();
builder.Services.AddSingleton<IIngestionService, IngestionService>();

builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:4173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (args.Contains("--scrape"))
{
    using var scope = app.Services.CreateScope();
    var scraper = scope.ServiceProvider.GetRequiredService<LawScraper>();
    await scraper.ScrapeAsync(Console.WriteLine);
    return;
}

if (args.Contains("--ingest"))
{
    using var scope = app.Services.CreateScope();
    var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionService>();
    var scrapeFirst = !args.Contains("--no-scrape");
    var result = await ingestion.IngestAsync(scrapeFirst, Console.WriteLine);
    Console.WriteLine($"Ingested {result.Laws} laws, {result.Chunks} chunks.");
    return;
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("frontend");
app.MapControllers();

app.Run();
