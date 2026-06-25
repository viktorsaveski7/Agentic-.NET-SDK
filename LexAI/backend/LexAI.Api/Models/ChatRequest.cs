namespace LexAI.Api.Models;

public class ChatRequest
{
    public string Question { get; set; } = string.Empty;
    public List<ConversationTurn>? ConversationHistory { get; set; }
}

public class ConversationTurn
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
}
