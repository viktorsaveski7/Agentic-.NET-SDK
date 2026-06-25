import { useState } from "react";
import type { FormEvent } from "react";
import { Loader2, Search } from "lucide-react";
import SourceCard from "../components/SourceCard";
import type { Source } from "../types";

export default function SearchPage() {
  const [query, setQuery] = useState("");
  const [submitted, setSubmitted] = useState("");
  const [results, setResults] = useState<Source[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasSearched, setHasSearched] = useState(false);

  const runSearch = async (e: FormEvent) => {
    e.preventDefault();
    const q = query.trim();
    if (!q || loading) return;

    setLoading(true);
    setError(null);
    setSubmitted(q);
    try {
      const res = await fetch("/api/chat/search", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ query: q, topK: 10 }),
      });
      if (!res.ok) throw new Error(`Грешка ${res.status}`);
      setResults((await res.json()) as Source[]);
      setHasSearched(true);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex h-full flex-col">
      <header className="border-b border-border px-6 py-4">
        <h1 className="text-sm font-semibold text-text-primary">
          Семантичко пребарување
        </h1>
        <p className="text-xs text-text-secondary">
          Директно пребарување низ векторската база, без AI одговор.
        </p>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-3xl px-4 py-6">
          <form onSubmit={runSearch} className="mb-6">
            <div className="flex items-center gap-2 rounded-2xl border border-border bg-surface px-3 py-2 focus-within:border-accent">
              <Search size={18} className="flex-none text-text-secondary" />
              <input
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Пребарувајте поими, членови, закони…"
                className="flex-1 bg-transparent py-1.5 text-sm text-text-primary outline-none placeholder:text-text-secondary"
              />
              <button
                type="submit"
                disabled={loading || query.trim().length === 0}
                className="flex h-9 items-center gap-1.5 rounded-lg bg-accent px-3 text-sm text-white transition-colors hover:bg-accent-hover disabled:opacity-40"
              >
                {loading ? (
                  <Loader2 size={16} className="animate-spin" />
                ) : (
                  "Пребарај"
                )}
              </button>
            </div>
          </form>

          {error && (
            <div className="mb-4 rounded-lg border border-low/40 bg-low/10 px-4 py-3 text-sm text-low">
              {error}
            </div>
          )}

          {hasSearched && !loading && results.length === 0 && !error && (
            <p className="text-sm text-text-secondary">
              Нема резултати. Дали базата е пополнета преку „Документи“?
            </p>
          )}

          <div className="space-y-3">
            {results.map((r, i) => (
              <SourceCard key={i} source={r} query={submitted} />
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
