# LexAI .NET — Agent Build Prompt

You are building **LexAI**, a Macedonian legal AI assistant. This is a full-stack application
with a .NET 8 Web API backend and a React + TypeScript frontend in the same repository.

The user already has a working Python version of this app. Your job is to rebuild it in .NET,
keeping the same architecture and features, but using idiomatic .NET tooling.

---

## What the App Does

A user types a question in Macedonian (e.g. "Што е договор за продажба?"). The system:

1. Scrapes Macedonian law PDFs from pravda.gov.mk
2. Extracts text from those PDFs
3. Chunks the text into ~1000 character pieces
4. Embeds each chunk using a multilingual embedding model running locally via Ollama
5. Stores the embeddings in ChromaDB (or Qdrant — see note below)
6. At query time: embeds the user question, searches the vector DB for top-5 most relevant chunks
7. Sends those chunks + the question to an LLM (Claude or GPT-4o)
8. Streams the answer back to the frontend with source citations

This pattern is called RAG — Retrieval-Augmented Generation.

---

## Repository Structure

Create the following folder structure in a single repo:

```
LexAI/
├── backend/                          # .NET 8 Web API
│   ├── LexAI.sln
│   ├── LexAI.Api/
│   │   ├── LexAI.Api.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Controllers/
│   │   │   └── ChatController.cs
│   │   ├── Services/
│   │   │   ├── IEmbeddingService.cs
│   │   │   ├── OllamaEmbeddingService.cs
│   │   │   ├── IVectorStoreService.cs
│   │   │   ├── ChromaVectorStoreService.cs
│   │   │   ├── IPdfProcessorService.cs
│   │   │   ├── PdfProcessorService.cs
│   │   │   ├── IChunkingService.cs
│   │   │   ├── ChunkingService.cs
│   │   │   ├── ILlmService.cs
│   │   │   └── LlmService.cs
│   │   ├── Models/
│   │   │   ├── ChatRequest.cs
│   │   │   ├── ChatResponse.cs
│   │   │   ├── DocumentChunk.cs
│   │   │   └── SearchResult.cs
│   │   └── Scripts/
│   │       └── ScrapeLaws.cs         # One-time ingestion script (called via CLI arg)
├── frontend/                         # React + TypeScript + Vite
│   ├── package.json
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── index.html
│   └── src/
│       ├── main.tsx
│       ├── App.tsx
│       ├── pages/
│       │   ├── ChatPage.tsx
│       │   ├── SearchPage.tsx
│       │   └── DocumentsPage.tsx
│       ├── components/
│       │   ├── Sidebar.tsx
│       │   ├── ChatMessage.tsx
│       │   ├── SourceCard.tsx
│       │   └── ConfidenceBar.tsx
│       ├── hooks/
│       │   └── useStreamingChat.ts
│       └── types/
│           └── index.ts
├── docker-compose.yml                # Only for ChromaDB — NOT for the app itself
└── README.md
```

---

## Prerequisites the User Must Have Installed

Before writing any code, tell the user to install:

1. **.NET 8 SDK** — https://dotnet.microsoft.com/download
2. **Node.js 20+** — https://nodejs.org
3. **Ollama** (native binary, NOT Docker):
   - Windows: Download OllamaSetup.exe from https://ollama.com/download/windows
   - Linux: `curl -fsSL https://ollama.com/install.sh | sh`
   - macOS: `brew install ollama`
   - After installing, pull the embedding model: `ollama pull nomic-embed-text`
   - Also pull an LLM if using local: `ollama pull llama3.2` (or use Claude/OpenAI API key)
4. **ChromaDB** via Docker (only the DB, not the whole app):
   ```bash
   docker run -d -p 8001:8000 --name chromadb chromadb/chroma
   ```
   Alternatively, ChromaDB can also be installed as a Python package and run natively.
   If the user doesn't want Docker at all, suggest **Qdrant** instead:
   - Qdrant native binary for Windows/Linux: https://qdrant.tech/documentation/quick-start/
   - Single executable, no Docker needed

---

## Backend — Step by Step

### NuGet Packages to Install

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.*" />
<PackageReference Include="OllamaSharp" Version="*" />
<PackageReference Include="Microsoft.Extensions.AI" Version="*" />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="*" />
<PackageReference Include="Anthropic.SDK" Version="*" />
<PackageReference Include="PdfPig" Version="*" />
<PackageReference Include="HtmlAgilityPack" Version="*" />
<PackageReference Include="ChromaDB.Client" Version="*" />
```

If using Qdrant instead of ChromaDB:
```xml
<PackageReference Include="Qdrant.Client" Version="*" />
```

### Step 1 — PDF Scraping (ScrapeLaws.cs)

Replaces: `backend/scripts/scrape_laws.py`

Use `HtmlAgilityPack` to fetch and parse the Ministry of Justice webpage, find all PDF links,
and download each PDF to a local `data/pdfs/` folder.

```
Target URL: https://www.pravda.gov.mk/mk-MK/regulativa/zakoni
Look for: <a href="..."> tags where href ends with .pdf
Download each PDF with HttpClient
Save to: data/pdfs/{filename}.pdf
```

Run as: `dotnet run --scrape` (check args in Program.cs and branch accordingly)

### Step 2 — PDF Text Extraction (PdfProcessorService.cs)

Replaces: `backend/app/services/pdf_processor.py`

Use **UglyToad.PdfPig** (NuGet: `PdfPig`) to extract text page by page.

```csharp
// PdfPig usage:
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

using var document = PdfDocument.Open("law.pdf");
foreach (var page in document.GetPages())
{
    string text = string.Join(" ", page.GetWords().Select(w => w.Text));
}
```

Implement the same garbled-text detection as the Python version:
- Count Cyrillic characters (Unicode range \u0400–\u04FF)
- If Cyrillic / total letters < 0.30, mark the page as garbled and skip it
- Log skipped pages

### Step 3 — Text Chunking (ChunkingService.cs)

Replaces: LangChain's RecursiveCharacterTextSplitter

Implement chunking manually (no LangChain equivalent needed — it's simple logic):

```csharp
public List<string> ChunkText(string text, int chunkSize = 1000, int overlap = 200)
{
    // Split on paragraph breaks first (\n\n), then lines (\n), then sentences (.), then words
    // Each chunk: ~1000 chars
    // Overlap: carry the last 200 chars into the next chunk
    // Return list of chunk strings
}
```

The separators to try in order: `"\n\n"`, `"\n"`, `"."`, `";"`, `","`, `" "`

### Step 4 — Embeddings (OllamaEmbeddingService.cs)

Replaces: HuggingFaceEmbeddings with multilingual-e5-base

Use **OllamaSharp** which implements `IEmbeddingGenerator<string, Embedding<float>>` from
`Microsoft.Extensions.AI`.

```csharp
using OllamaSharp;
using Microsoft.Extensions.AI;

// In DI registration (Program.cs):
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    new OllamaApiClient(new Uri("http://localhost:11434"), "nomic-embed-text")
);

// Usage:
var vector = await _embeddingGenerator.GenerateVectorAsync("passage: " + chunkText);
```

**Important:** The multilingual-e5 model requires prefixes:
- Use `"passage: "` prefix when embedding document chunks
- Use `"query: "` prefix when embedding user questions

`nomic-embed-text` (the Ollama default) does NOT require prefixes — it works without them.
Recommend using `nomic-embed-text` for simplicity, or `mxbai-embed-large` for better quality.

Embeddings are always normalized (L2 norm = 1.0) so cosine similarity = dot product.

### Step 5 — Vector Store (ChromaVectorStoreService.cs)

Replaces: ChromaDB via langchain_chroma

Use **ChromaDB.Client** NuGet package to connect to the locally running ChromaDB instance.

```csharp
// Collection name: "macedonian_laws"
// Store each chunk with:
//   - id: "{lawName}_{chunkIndex}"
//   - embedding: float[] from Ollama
//   - metadata: { "law": "...", "page": N, "chunk": N }
//   - document: the chunk text itself

// Search:
// 1. Embed the user query with "query: " prefix
// 2. Call collection.Query(queryEmbeddings, nResults: 5)
// 3. Return top 5 chunks with their distances
// 4. Cosine distance → similarity score = 1 - distance
```

If using Qdrant instead, use `Qdrant.Client` — the API is similar but uses `PointStruct` for
upsert and `SearchAsync` for queries. Use `VectorParams` with `Distance.Cosine`.

### Step 6 — LLM Service (LlmService.cs)

Replaces: the OpenAI/Claude call in llm_service.py

Support both Claude (via `Anthropic.SDK`) and OpenAI (via `Microsoft.Extensions.AI.OpenAI`).
Read the provider choice from `appsettings.json`.

**System prompt to use:**
```
Ti si pravен асистент специјализиран за македонско законодавство.
Одговарај САМО врз основа на дадениот контекст од македонските закони.
Ако одговорот не се наоѓа во контекстот, кажи "Не можам да најдам релевантни информации 
во достапните закони."
Секогаш цитирај конкретни членови и закони.
Одговарај на јазикот на прашањето (македонски).
```

**Context building:**
```
Контекст од македонските закони:

[CHUNK 1 - {law_name}, Страница {page}]
{chunk_text}

[CHUNK 2 - {law_name}, Страница {page}]
{chunk_text}

...

Прашање: {user_question}
```

**Streaming:** Use `IAsyncEnumerable<string>` for streaming tokens back.
- For Claude: use `Anthropic.SDK`'s streaming API
- For OpenAI: use `IChatClient.GetStreamingResponseAsync()`

### Step 7 — API Controller (ChatController.cs)

```csharp
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request)
    // Returns: ChatResponse with answer + sources + confidence score

    [HttpPost("ask/stream")]
    public async Task AskStream([FromBody] ChatRequest request)
    // Returns: SSE stream
    // Content-Type: text/event-stream
    // Events:
    //   data: {"type":"sources","sources":[...],"confidence":0.79}
    //   data: {"type":"token","content":"Според"}
    //   data: {"type":"done"}

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest()
    // Triggers the full ingestion pipeline: scrape → extract → chunk → embed → store
    // Returns progress updates
    
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });
}
```

**Confidence score calculation:**
```csharp
// Take the top similarity score from vector search
// Map it: score >= 0.8 → high, 0.6-0.8 → medium, < 0.6 → low
// Return as float between 0-1
```

### Step 8 — appsettings.json

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text"
  },
  "ChromaDB": {
    "BaseUrl": "http://localhost:8001",
    "CollectionName": "macedonian_laws"
  },
  "LLM": {
    "Provider": "Claude",
    "AnthropicApiKey": "",
    "OpenAIApiKey": "",
    "Model": "claude-haiku-4-5-20251001"
  },
  "Ingestion": {
    "PdfDirectory": "./data/pdfs",
    "ChunkSize": 1000,
    "ChunkOverlap": 200,
    "SourceUrl": "https://www.pravda.gov.mk/mk-MK/regulativa/zakoni"
  },
  "AllowedHosts": "*",
  "Urls": "http://localhost:5000"
}
```

### Step 9 — Program.cs

```csharp
// Register all services
// Configure CORS to allow the React frontend (http://localhost:5173)
// Add Swagger/OpenAPI
// If args contains "--scrape", run the scraper and exit
// If args contains "--ingest", run the full ingestion pipeline and exit
// Otherwise, start the web server
```

---

## Frontend — Step by Step

### Setup

```bash
cd frontend
npm create vite@latest . -- --template react-ts
npm install tailwindcss @tailwindcss/vite
npm install react-markdown
npm install lucide-react
npm install react-router-dom
```

### Design Direction

The app is for Macedonian lawyers — serious professionals who need to trust the tool.

Design tokens:
- Background: `#0F1117` (near-black, serious)
- Surface: `#1A1D27` (card/panel background)
- Border: `#2A2D3E`
- Accent: `#6366F1` (indigo — judicial authority, not playful)
- Accent hover: `#818CF8`
- Text primary: `#F1F5F9`
- Text secondary: `#94A3B8`
- Success/high confidence: `#10B981`
- Warning/medium confidence: `#F59E0B`
- Error/low confidence: `#EF4444`
- Font: `Inter` for UI, `JetBrains Mono` for law article citations

Signature element: a subtle animated indigo gradient behind the chat header that pulses
slowly — evoking the "always searching" nature of the system.

### Pages

**ChatPage.tsx** — Main interface:
- Left sidebar: conversation history, navigation links, dark mode toggle (always on — dark only)
- Main area: chat messages with markdown rendering
- Each assistant message shows:
  - The answer (rendered markdown)
  - Collapsible "Sources" section showing law name, page, relevance score
  - Confidence indicator (colored bar: green/yellow/red)
  - Copy to clipboard button
  - 3 suggested follow-up questions (from LLM)
- Input: textarea at the bottom, sends on Enter (Shift+Enter for newline), loading spinner
- Streaming: words appear one by one as they arrive via SSE

**SearchPage.tsx** — Direct semantic search:
- Search bar at top
- Results as cards showing: law name, chunk text with query terms highlighted, similarity score
- No LLM call — just raw vector search results

**DocumentsPage.tsx** — Ingestion management:
- Show count of indexed laws and chunks
- "Re-ingest All Laws" button that calls POST /api/chat/ingest
- Progress log showing ingestion status
- List of indexed law names

### Streaming Hook (useStreamingChat.ts)

```typescript
// useStreamingChat hook:
// - Takes: question string
// - Returns: { sources, tokens, confidence, isStreaming, error }
// - Calls: POST /api/chat/ask/stream
// - Reads SSE events:
//   { type: "sources", sources: [...], confidence: 0.79 }
//   { type: "token", content: "Според" }
//   { type: "done" }
// - Accumulates tokens into a single string
// - Updates state on each event
```

### Types (types/index.ts)

```typescript
interface Source {
  lawName: string;
  page: number;
  chunk: string;
  score: number;
}

interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  sources?: Source[];
  confidence?: number;
  timestamp: Date;
}

interface ChatRequest {
  question: string;
  conversationHistory?: { role: string; content: string }[];
}
```

### Vite Proxy (vite.config.ts)

```typescript
// Proxy /api/* to http://localhost:5000
// This avoids CORS issues during development
```

---

## Docker Compose (for ChromaDB only)

```yaml
version: "3.8"
services:
  chromadb:
    image: chromadb/chroma
    ports:
      - "8001:8000"
    volumes:
      - chromadb_data:/chroma/chroma
    environment:
      - ANONYMIZED_TELEMETRY=false

volumes:
  chromadb_data:
```

Run with: `docker compose up -d chromadb`

Note: The .NET app and React app are NOT in Docker — they run natively.

---

## How To Run Everything

```bash
# 1. Start ChromaDB (only needed once)
docker compose up -d chromadb

# 2. Make sure Ollama is running (it starts automatically after install)
ollama list   # Should show nomic-embed-text

# 3. Backend — first time ingestion
cd backend
dotnet run --ingest   # Downloads PDFs, extracts text, chunks, embeds, stores in ChromaDB

# 4. Backend — normal run
dotnet run

# 5. Frontend
cd frontend
npm run dev
# Open http://localhost:5173
```

---

## Key Implementation Notes

### Cyrillic Detection (C# version of Python's _is_garbled)

```csharp
private bool IsGarbled(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return true;
    int cyrillicCount = text.Count(c => c >= '\u0400' && c <= '\u04FF');
    int letterCount = text.Count(char.IsLetter);
    if (letterCount == 0) return true;
    return (double)cyrillicCount / letterCount < 0.30;
}
```

### SSE Streaming in ASP.NET Core

```csharp
Response.Headers.Append("Content-Type", "text/event-stream");
Response.Headers.Append("Cache-Control", "no-cache");
Response.Headers.Append("X-Accel-Buffering", "no");

await foreach (var token in _llmService.StreamAsync(question, chunks))
{
    var json = JsonSerializer.Serialize(new { type = "token", content = token });
    await Response.WriteAsync($"data: {json}\n\n");
    await Response.Body.FlushAsync();
}
await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { type = "done" })}\n\n");
```

### Cosine Similarity Score

ChromaDB returns distances (lower = more similar). Convert to similarity score:
```csharp
double similarity = 1.0 - distance;  // For cosine distance
```

### Dependency Injection Order in Program.cs

```csharp
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    new OllamaApiClient(new Uri(config["Ollama:BaseUrl"]), config["Ollama:EmbeddingModel"])
);
builder.Services.AddSingleton<IVectorStoreService, ChromaVectorStoreService>();
builder.Services.AddSingleton<IPdfProcessorService, PdfProcessorService>();
builder.Services.AddSingleton<IChunkingService, ChunkingService>();
builder.Services.AddSingleton<ILlmService, LlmService>();
builder.Services.AddHttpClient(); // For scraping
builder.Services.AddCors(...);
builder.Services.AddControllers();
```

---

## What is NOT in Scope (for this first version)

- User authentication
- Conversation history persistence (in-memory only per session)
- PDF upload UI (ingestion is triggered via API or CLI)
- The agentic quality-check / retry loop (that is the next project)
- Multi-tenancy

---

## Summary of Python → .NET Mapping

| Python Component | .NET Equivalent |
|---|---|
| `requests` + `BeautifulSoup4` | `HttpClient` + `HtmlAgilityPack` |
| `pdfplumber` | `PdfPig` (UglyToad.PdfPig) |
| `RecursiveCharacterTextSplitter` | Custom `ChunkingService` (simple, ~30 lines) |
| `HuggingFaceEmbeddings` (multilingual-e5) | `OllamaSharp` with `nomic-embed-text` via `IEmbeddingGenerator` |
| `langchain_chroma` (ChromaDB) | `ChromaDB.Client` NuGet |
| OpenAI / Claude SDK | `Anthropic.SDK` or `Microsoft.Extensions.AI.OpenAI` |
| FastAPI | ASP.NET Core 8 Web API |
| SSE streaming | `Response.WriteAsync` with `text/event-stream` |
| `uvicorn` | `dotnet run` (Kestrel built-in) |
| `react` frontend | Same React stack, same structure |

---

## Start Here

When the user runs this prompt, build in this order:

1. Create the solution and project structure
2. Install all NuGet packages
3. Implement `PdfProcessorService` and `ChunkingService` (no external deps, easy to test)
4. Implement `OllamaEmbeddingService`
5. Implement `ChromaVectorStoreService`
6. Implement `LlmService` with streaming
7. Implement `ChatController`
8. Implement `ScrapeLaws` ingestion script
9. Wire everything in `Program.cs`
10. Create the React frontend (Vite + TypeScript + Tailwind)
11. Build the dark-themed UI with chat, search, and documents pages
12. Set up the Vite proxy to the .NET backend
13. Write the README with exact run commands

Build one thing at a time, verify it compiles, then move to the next.
Do not scaffold everything at once and leave things unimplemented.
