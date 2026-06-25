using HtmlAgilityPack;

namespace LexAI.Api.Scripts;

public class LawScraper
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<LawScraper> _logger;

    public LawScraper(IHttpClientFactory httpFactory, IConfiguration config, ILogger<LawScraper> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<List<string>> ScrapeAsync(Action<string>? progress = null, CancellationToken ct = default)
    {
        var sourceUrl = _config["Ingestion:SourceUrl"]
                        ?? "https://www.pravda.gov.mk/mk-MK/regulativa/zakoni";
        var pdfDir = _config["Ingestion:PdfDirectory"] ?? "./data/pdfs";
        Directory.CreateDirectory(pdfDir);

        void Report(string msg) { _logger.LogInformation("{Message}", msg); progress?.Invoke(msg); }

        Report($"Fetching law index from {sourceUrl}");

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LexAI-Scraper/1.0");

        var html = await http.GetStringAsync(sourceUrl, ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var baseUri = new Uri(sourceUrl);
        var pdfLinks = doc.DocumentNode
            .SelectNodes("//a[@href]")?
            .Select(a => a.GetAttributeValue("href", string.Empty))
            .Where(href => href.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Select(href => new Uri(baseUri, href))
            .Distinct()
            .ToList() ?? new List<Uri>();

        Report($"Found {pdfLinks.Count} PDF links");

        var saved = new List<string>();
        foreach (var uri in pdfLinks)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = SanitizeFileName(Path.GetFileName(uri.LocalPath));
            if (string.IsNullOrWhiteSpace(fileName)) continue;

            var target = Path.Combine(pdfDir, fileName);
            if (File.Exists(target))
            {
                saved.Add(target);
                continue;
            }

            try
            {
                Report($"Downloading {fileName}");
                var bytes = await http.GetByteArrayAsync(uri, ct);
                await File.WriteAllBytesAsync(target, bytes, ct);
                saved.Add(target);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download {Uri}", uri);
                Report($"Failed to download {fileName}: {ex.Message}");
            }
        }

        Report($"Saved {saved.Count} PDFs to {pdfDir}");
        return saved;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
