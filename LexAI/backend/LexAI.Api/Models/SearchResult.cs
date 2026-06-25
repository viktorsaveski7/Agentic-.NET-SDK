namespace LexAI.Api.Models;

public class SearchResult
{
    public string Id { get; set; } = string.Empty;
    public string LawName { get; set; } = string.Empty;
    public int Page { get; set; }
    public int ChunkIndex { get; set; }
    public string Document { get; set; } = string.Empty;
    public double Distance { get; set; }
    public double Score => 1.0 - Distance;
}
