using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using LexAI.Api.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LexAI.Api.Services;

public class LlmService : ILlmService
{
    private const string SystemPrompt =
        "Ti si pravен асистент специјализиран за македонско законодавство. " +
        "Одговарај САМО врз основа на дадениот контекст од македонските закони. " +
        "Ако одговорот не се наоѓа во контекстот, кажи \"Не можам да најдам релевантни информации во достапните закони.\" " +
        "Секогаш цитирај конкретни членови и закони. " +
        "Одговарај на јазикот на прашањето (македонски).";

    private readonly string _model;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly object _clientLock = new();
    private IChatClient? _client;
    private readonly ILogger<LlmService> _logger;

    public LlmService(IConfiguration configuration, ILogger<LlmService> logger)
    {
        _logger = logger;
        _model = configuration["LLM:Model"] ?? "deepseek-chat";
        _baseUrl = configuration["LLM:BaseUrl"] ?? "https://api.deepseek.com";
        _apiKey = configuration["LLM:ApiKey"] ?? string.Empty;
    }

    private IChatClient Client
    {
        get
        {
            if (_client is not null) return _client;
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException(
                    "DeepSeek API key is not configured. Set \"LLM:ApiKey\" in appsettings.json and restart the backend.");
            lock (_clientLock)
            {
                _client ??= new ChatClient(_model, new ApiKeyCredential(_apiKey),
                    new OpenAIClientOptions { Endpoint = new Uri(_baseUrl) }).AsIChatClient();
            }
            return _client;
        }
    }

    public IAsyncEnumerable<string> StreamAsync(
        string question,
        IReadOnlyList<SearchResult> context,
        IReadOnlyList<ConversationTurn>? history,
        CancellationToken ct = default)
    {
        var userPrompt = BuildUserPrompt(question, context);
        return StreamRawAsync(SystemPrompt, history, userPrompt, ct);
    }

    public async Task<List<string>> GenerateFollowUpsAsync(string question, string answer, CancellationToken ct = default)
    {
        const string system =
            "Ти си помошник кој предлага дополнителни прашања за македонско законодавство. " +
            "Врз основа на прашањето и одговорот, предложи точно 3 кратки дополнителни прашања на македонски. " +
            "Врати само по едно прашање во секој ред, без нумерирање и без дополнителен текст.";

        var prompt = $"Прашање: {question}\n\nОдговор: {answer}\n\nПредложи 3 дополнителни прашања:";

        try
        {
            var sb = new StringBuilder();
            await foreach (var token in StreamRawAsync(system, null, prompt, ct))
                sb.Append(token);

            return sb.ToString()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(CleanLine)
                .Where(l => l.Length > 0)
                .Take(3)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate follow-up questions");
            return new List<string>();
        }
    }

    private async IAsyncEnumerable<string> StreamRawAsync(
        string system,
        IReadOnlyList<ConversationTurn>? history,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, system) };
        if (history is not null)
            foreach (var turn in history)
                messages.Add(new ChatMessage(ChatRoleOf(turn.Role), turn.Content));
        messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        var options = new ChatOptions { ModelId = _model, MaxOutputTokens = 1500, Temperature = 0.2f };

        await foreach (var update in Client.GetStreamingResponseAsync(messages, options, ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    private static string BuildUserPrompt(string question, IReadOnlyList<SearchResult> context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Контекст од македонските закони:");
        sb.AppendLine();

        for (int i = 0; i < context.Count; i++)
        {
            var c = context[i];
            sb.AppendLine($"[CHUNK {i + 1} - {c.LawName}, Страница {c.Page}]");
            sb.AppendLine(c.Document);
            sb.AppendLine();
        }

        sb.AppendLine($"Прашање: {question}");
        return sb.ToString();
    }

    private static ChatRole ChatRoleOf(string role) =>
        role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant : ChatRole.User;

    private static string CleanLine(string line) =>
        line.TrimStart('-', '*', '•', ' ', '\t', '1', '2', '3', '.', ')').Trim();
}
