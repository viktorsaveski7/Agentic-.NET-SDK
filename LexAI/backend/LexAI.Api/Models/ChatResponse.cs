namespace LexAI.Api.Models;

public class Source
{
    public string LawName { get; set; } = string.Empty;
    public int Page { get; set; }
    public string Chunk { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<Source> Sources { get; set; } = new();
    public double Confidence { get; set; }
    public List<string> FollowUps { get; set; } = new();
}
