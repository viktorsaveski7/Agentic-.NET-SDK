using LexAI.Api.Models;

namespace LexAI.Api.Services;

public interface ILlmService
{
    IAsyncEnumerable<string> StreamAsync(
        string question,
        IReadOnlyList<SearchResult> context,
        IReadOnlyList<ConversationTurn>? history,
        CancellationToken ct = default);

    Task<List<string>> GenerateFollowUpsAsync(
        string question,
        string answer,
        CancellationToken ct = default);
}
