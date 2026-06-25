import { useCallback, useEffect, useState } from "react";
import { Database, FileText, Layers, Loader2, RefreshCw } from "lucide-react";
import type { DocumentStats, IngestionResult } from "../types";

export default function DocumentsPage() {
  const [stats, setStats] = useState<DocumentStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [ingesting, setIngesting] = useState(false);
  const [log, setLog] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);

  const loadStats = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/chat/documents");
      if (!res.ok) throw new Error(`Грешка ${res.status}`);
      setStats((await res.json()) as DocumentStats);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadStats();
  }, [loadStats]);

  const reingest = async () => {
    if (ingesting) return;
    setIngesting(true);
    setError(null);
    setLog(["Започнува внес на закони… ова може да потрае неколку минути."]);
    try {
      const res = await fetch("/api/chat/ingest?scrape=true", {
        method: "POST",
      });
      if (!res.ok) throw new Error(`Грешка ${res.status}`);
      const result = (await res.json()) as IngestionResult;
      setLog(result.log);
      await loadStats();
    } catch (err) {
      setError((err as Error).message);
      setLog((prev) => [...prev, `Грешка: ${(err as Error).message}`]);
    } finally {
      setIngesting(false);
    }
  };

  return (
    <div className="flex h-full flex-col">
      <header className="flex items-center justify-between border-b border-border px-6 py-4">
        <div>
          <h1 className="text-sm font-semibold text-text-primary">Документи</h1>
          <p className="text-xs text-text-secondary">
            Управување со индексирани закони.
          </p>
        </div>
        <button
          onClick={reingest}
          disabled={ingesting}
          className="flex items-center gap-2 rounded-lg bg-accent px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-accent-hover disabled:opacity-50"
        >
          {ingesting ? (
            <Loader2 size={16} className="animate-spin" />
          ) : (
            <RefreshCw size={16} />
          )}
          Внеси ги сите закони
        </button>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-3xl px-4 py-6">
          {error && (
            <div className="mb-4 rounded-lg border border-low/40 bg-low/10 px-4 py-3 text-sm text-low">
              {error}
            </div>
          )}

          <div className="mb-6 grid grid-cols-2 gap-3">
            <div className="rounded-xl border border-border bg-surface p-4">
              <div className="mb-1 flex items-center gap-2 text-text-secondary">
                <Database size={16} /> <span className="text-xs">Закони</span>
              </div>
              <div className="text-2xl font-semibold text-text-primary">
                {loading ? "…" : stats?.laws ?? 0}
              </div>
            </div>
            <div className="rounded-xl border border-border bg-surface p-4">
              <div className="mb-1 flex items-center gap-2 text-text-secondary">
                <Layers size={16} /> <span className="text-xs">Парчиња</span>
              </div>
              <div className="text-2xl font-semibold text-text-primary">
                {loading ? "…" : stats?.chunks ?? 0}
              </div>
            </div>
          </div>

          {log.length > 0 && (
            <div className="mb-6">
              <h2 className="mb-2 text-sm font-semibold text-text-primary">
                Дневник на внес
              </h2>
              <div className="max-h-64 overflow-y-auto rounded-xl border border-border bg-bg p-3 font-mono text-xs leading-relaxed text-text-secondary">
                {log.map((line, i) => (
                  <div key={i}>{line}</div>
                ))}
              </div>
            </div>
          )}

          <div>
            <h2 className="mb-2 text-sm font-semibold text-text-primary">
              Индексирани закони
            </h2>
            {stats && stats.lawNames.length > 0 ? (
              <div className="space-y-1">
                {stats.lawNames.map((name) => (
                  <div
                    key={name}
                    className="flex items-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-text-secondary"
                  >
                    <FileText size={14} className="flex-none text-accent-hover" />
                    <span className="truncate font-mono text-xs">{name}</span>
                  </div>
                ))}
              </div>
            ) : (
              !loading && (
                <p className="text-sm text-text-secondary">
                  Сè уште нема индексирани закони. Кликнете „Внеси ги сите
                  закони“.
                </p>
              )
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
