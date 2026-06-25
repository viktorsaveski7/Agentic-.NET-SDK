namespace LexAI.Api.Services;

public record ExtractedPage(int Page, string Text);

public interface IPdfProcessorService
{
    List<ExtractedPage> ExtractPages(string pdfPath);
}
