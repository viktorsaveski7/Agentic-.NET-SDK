import type { FC } from "react";
import { NavLink } from "react-router-dom";
import {
  FileText,
  MessageSquare,
  Moon,
  Plus,
  Scale,
  Search,
} from "lucide-react";

export interface HistoryItem {
  id: string;
  title: string;
}

interface Props {
  history: HistoryItem[];
  onSelectHistory: (id: string) => void;
  onNewChat: () => void;
}

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  [
    "flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors",
    isActive
      ? "bg-accent/15 text-accent-hover"
      : "text-text-secondary hover:bg-surface hover:text-text-primary",
  ].join(" ");

const Sidebar: FC<Props> = ({ history, onSelectHistory, onNewChat }) => {
  return (
    <aside className="flex h-full w-64 flex-none flex-col border-r border-border bg-surface/50">
      <div className="flex items-center gap-2 px-4 py-4">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-accent text-white">
          <Scale size={18} />
        </div>
        <div>
          <div className="text-sm font-semibold text-text-primary">LexAI</div>
          <div className="text-xs text-text-secondary">Правен асистент</div>
        </div>
      </div>

      <div className="px-3">
        <button
          onClick={onNewChat}
          className="flex w-full items-center justify-center gap-2 rounded-lg border border-border bg-bg/40 px-3 py-2 text-sm font-medium text-text-primary transition-colors hover:border-accent"
        >
          <Plus size={16} /> Нов разговор
        </button>
      </div>

      <nav className="mt-4 space-y-1 px-3">
        <NavLink to="/" end className={navLinkClass}>
          <MessageSquare size={16} /> Разговор
        </NavLink>
        <NavLink to="/search" className={navLinkClass}>
          <Search size={16} /> Пребарување
        </NavLink>
        <NavLink to="/documents" className={navLinkClass}>
          <FileText size={16} /> Документи
        </NavLink>
      </nav>

      <div className="mt-6 min-h-0 flex-1 overflow-y-auto px-3">
        {history.length > 0 && (
          <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-text-secondary">
            Историја
          </div>
        )}
        <div className="space-y-0.5">
          {history.map((item) => (
            <button
              key={item.id}
              onClick={() => onSelectHistory(item.id)}
              className="block w-full truncate rounded-lg px-3 py-1.5 text-left text-sm text-text-secondary transition-colors hover:bg-surface hover:text-text-primary"
              title={item.title}
            >
              {item.title}
            </button>
          ))}
        </div>
      </div>

      <div className="border-t border-border px-3 py-3">
        <button
          disabled
          className="flex w-full cursor-default items-center justify-between rounded-lg px-3 py-2 text-sm text-text-secondary"
        >
          <span className="flex items-center gap-2">
            <Moon size={16} /> Темна тема
          </span>
          <span className="text-xs text-accent-hover">вклучена</span>
        </button>
      </div>
    </aside>
  );
};

export default Sidebar;
