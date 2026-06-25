import type { FC, ReactNode } from "react";
import type { Source } from "../types";

interface Props {
  source: Source;
  query?: string;
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function highlight(text: string, query?: string): ReactNode {
  if (!query) return text;
  const terms = query
    .split(/\s+/)
    .map((t) => t.trim())
    .filter((t) => t.length > 2)
    .map(escapeRegExp);
  if (terms.length === 0) return text;

  const splitRegex = new RegExp(`(${terms.join("|")})`, "gi");
  const matchRegex = new RegExp(`^(?:${terms.join("|")})$`, "i");
  const parts = text.split(splitRegex);
  return parts.map((part, i) =>
    matchRegex.test(part) ? (
      <mark key={i} className="rounded bg-accent/30 px-0.5 text-text-primary">
        {part}
      </mark>
    ) : (
      <span key={i}>{part}</span>
    )
  );
}

const SourceCard: FC<Props> = ({ source, query }) => {
  const scorePct = Math.round(source.score * 100);

  return (
    <div className="rounded-lg border border-border bg-bg/60 p-3">
      <div className="mb-1.5 flex items-center justify-between gap-2">
        <span className="truncate font-mono text-xs text-accent-hover">
          {source.lawName}
        </span>
        <span className="whitespace-nowrap text-xs text-text-secondary">
          стр. {source.page} · {scorePct}%
        </span>
      </div>
      <p className="max-h-40 overflow-y-auto text-sm leading-relaxed text-text-secondary">
        {highlight(source.chunk, query)}
      </p>
    </div>
  );
};

export default SourceCard;
