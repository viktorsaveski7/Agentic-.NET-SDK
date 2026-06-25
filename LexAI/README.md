# LexAI .NET — Macedonian Legal AI Assistant

A full-stack **RAG** (Retrieval-Augmented Generation) app for Macedonian
legislation. Ask a question in Macedonian; LexAI retrieves the most relevant law
chunks from a vector database and an LLM answers with cited sources.

- **Backend:** .NET 8 Web API (`backend/`)
- **Frontend:** React + TypeScript + Vite + Tailwind (`frontend/`)
- **Vector DB:** ChromaDB (via Docker)
- **Embeddings:** Ollama (`nomic-embed-text`) — runs locally
- **LLM:** DeepSeek (OpenAI-compatible API)

```
LexAI/
├── backend/            # .NET 8 Web API (LexAI.sln)
├── frontend/           # React + TypeScript (Vite)
├── docker-compose.yml  # ChromaDB only
└── README.md
```

---

## Prerequisites

1. **.NET SDK 8 or newer** — https://dotnet.microsoft.com/download
   (Built and verified with the .NET 9 SDK targeting `net8.0`.)
2. **Node.js** — https://nodejs.org
   The frontend is pinned to **Vite 4 + Tailwind 3**, so it works on **Node 16
   through 22**. (The repo was verified building on Node 16.)
3. **Ollama** (native binary, not Docker) — https://ollama.com/download
   ```bash
   ollama pull nomic-embed-text      # embedding model
   ```
4. **Docker** (for ChromaDB only).

---

## 1. Start ChromaDB

```bash
docker compose up -d chromadb
```

ChromaDB listens on **http://localhost:8001** (mapped from the container's 8000).

> The .NET client (`ChromaDB.Client` 1.0.0) talks to Chroma's tenant/database
> API. The compose file pins `chromadb/chroma:0.5.23`, a known-compatible
> server. If you change the tag and ingestion/search fails with HTTP errors,
> revert to a compatible Chroma version.

## 2. Make sure Ollama is running

```bash
ollama list      # should list nomic-embed-text
```

## 3. Configure the LLM

Edit `backend/LexAI.Api/appsettings.json` (or use user-secrets / env vars):

```json
"LLM": {
  "Provider": "DeepSeek",                 // informational only
  "BaseUrl": "https://api.deepseek.com",  // OpenAI-compatible endpoint
  "ApiKey": "",                           // your DeepSeek API key
  "Model": "deepseek-chat"                // set the exact model your account exposes
}
```

> LexAI talks to DeepSeek through its **OpenAI-compatible** API. Set `ApiKey` to
> your key and `Model` to the exact model identifier your DeepSeek account/gateway
> uses. If you call DeepSeek through a gateway (e.g. OpenRouter), set `BaseUrl`
> and `Model` accordingly.

Other relevant settings:

| Key | Default | Notes |
|---|---|---|
| `Ollama:BaseUrl` | `http://localhost:11434` | |
| `Ollama:EmbeddingModel` | `nomic-embed-text` | |
| `Ollama:UsePrefixes` | `false` | set `true` only for `multilingual-e5` models |
| `ChromaDB:BaseUrl` | `http://localhost:8001` | |
| `ChromaDB:CollectionName` | `macedonian_laws` | |
| `Ingestion:SourceUrl` | pravda.gov.mk laws page | |
| `Ingestion:ChunkSize` / `ChunkOverlap` | `1000` / `200` | |

## 4. Ingest the laws (first run only)

This scrapes the PDFs, extracts text, chunks it, embeds each chunk via Ollama,
and stores everything in ChromaDB:

```bash
cd backend
dotnet run --project LexAI.Api -- --ingest
```

Other CLI modes:

```bash
dotnet run --project LexAI.Api -- --scrape      # download PDFs only
dotnet run --project LexAI.Api -- --ingest --no-scrape   # ingest existing PDFs in data/pdfs
```

## 5. Run the backend

```bash
cd backend
dotnet run --project LexAI.Api
# API:     http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

## 6. Run the frontend

```bash
cd frontend
npm install
npm run dev
# open http://localhost:5173
```

The Vite dev server proxies `/api/*` to `http://localhost:5000`.

---

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/chat/ask` | Full answer + sources + confidence + follow-ups |
| `POST` | `/api/chat/ask/stream` | SSE stream (`sources`, `token`, `followups`, `done`) |
| `POST` | `/api/chat/search` | Raw vector search (no LLM) |
| `POST` | `/api/chat/ingest?scrape=true` | Run the ingestion pipeline |
| `GET`  | `/api/chat/documents` | Indexed law/chunk counts + names |
| `GET`  | `/api/chat/health` | Health check |

SSE event shapes:

```
data: {"type":"sources","sources":[{"lawName":"...","page":3,"chunk":"...","score":0.81}],"confidence":0.81}
data: {"type":"token","content":"Според"}
data: {"type":"followups","followUps":["...","...","..."]}
data: {"type":"done"}
```

---

## Frontend pages

- **Разговор (Chat):** streaming answers, collapsible sources, confidence bar,
  copy button, suggested follow-ups.
- **Пребарување (Search):** direct semantic search with highlighted matches.
- **Документи (Documents):** indexed counts, re-ingest button, ingestion log.

---

## Notes

- **Private NuGet feed:** `backend/nuget.config` clears inherited package sources
  and uses only nuget.org, so restore works even where a private/authenticated
  feed is configured machine-wide.
- **Tailwind version:** the original brief suggested Tailwind v4
  (`@tailwindcss/vite`, requires Node 20+). To stay compatible with Node 16 this
  repo uses Tailwind v3 + PostCSS. The design tokens are identical.
- **Not in scope (v1):** auth, persistent conversation history (in-memory per
  session), PDF upload UI, agentic retry loop, multi-tenancy.
