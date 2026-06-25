namespace LexAI.Api.Services;

public interface IChunkingService
{
    List<string> ChunkText(string text, int chunkSize = 1000, int overlap = 200);
}
