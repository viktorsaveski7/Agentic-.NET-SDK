namespace LexAI.Api.Models;

public class DocumentChunk
{
    public string LawName { get; set; } = string.Empty;
    public int Page { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;

    public string Id => $"{LawName}_{Page}_{ChunkIndex}";
}
