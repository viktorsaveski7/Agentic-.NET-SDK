using UglyToad.PdfPig;

namespace LexAI.Api.Services;

public class PdfProcessorService : IPdfProcessorService
{
    private readonly ILogger<PdfProcessorService> _logger;

    public PdfProcessorService(ILogger<PdfProcessorService> logger)
    {
        _logger = logger;
    }

    public List<ExtractedPage> ExtractPages(string pdfPath)
    {
        var pages = new List<ExtractedPage>();
        var lawName = Path.GetFileNameWithoutExtension(pdfPath);

        using var document = PdfDocument.Open(pdfPath);
        foreach (var page in document.GetPages())
        {
            var text = string.Join(" ", page.GetWords().Select(w => w.Text)).Trim();

            if (IsGarbled(text))
            {
                _logger.LogWarning("Skipping garbled page {Page} in {Law}", page.Number, lawName);
                continue;
            }

            pages.Add(new ExtractedPage(page.Number, text));
        }

        _logger.LogInformation("Extracted {Count} usable pages from {Law}", pages.Count, lawName);
        return pages;
    }

    private static bool IsGarbled(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        int cyrillicCount = text.Count(c => c >= '\u0400' && c <= '\u04FF');
        int letterCount = text.Count(char.IsLetter);
        if (letterCount == 0) return true;
        return (double)cyrillicCount / letterCount < 0.30;
    }
}
