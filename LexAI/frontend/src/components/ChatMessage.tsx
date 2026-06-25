import { useState } from "react";
import type { FC } from "react";
import ReactMarkdown from "react-markdown";
import {
  Check,
  ChevronDown,
  ChevronRight,
  Copy,
  Scale,
  Sparkles,
  User,
} from "lucide-react";
import type { ChatMessage as ChatMessageType } from "../types";
import ConfidenceBar from "./ConfidenceBar";
import SourceCard from "./SourceCard";

interface Props {
  message: ChatMessageType;
  onFollowUp: (question: string) => void;
}

const ChatMessage: FC<Props> = ({ message, onFollowUp }) => {
  const [sourcesOpen, setSourcesOpen] = useState(false);
  const [copied, setCopied] = useState(false);

  const isUser = message.role === "user";

  const copy = async () => {
    await navigator.clipboard.writeText(message.content);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  };

  if (isUser) {
    return (
      <div className="flex justify-end gap-3">
        <div className="max-w-[80%] rounded-2xl rounded-tr-sm bg-accent px-4 py-2.5 text-sm text-white">
          {message.content}
        </div>
        <div className="mt-0.5 flex h-8 w-8 flex-none items-center justify-center rounded-full bg-surface text-text-secondary">
          <User size={16} />
        </div>
      </div>
    );
  }

  const hasSources = !!message.sources && message.sources.length > 0;

  return (
    <div className="flex gap-3">
      <div className="mt-0.5 flex h-8 w-8 flex-none items-center justify-center rounded-full bg-accent/20 text-accent-hover">
        <Scale size={16} />
      </div>

      <div className="min-w-0 max-w-[85%] flex-1">
        <div className="rounded-2xl rounded-tl-sm border border-border bg-surface px-4 py-3">
          <div className="markdown text-text-primary">
            <ReactMarkdown>{message.content || "​"}</ReactMarkdown>
            {message.isStreaming && (
              <span className="ml-0.5 inline-block h-4 w-1.5 animate-pulse bg-accent align-middle" />
            )}
          </div>

          {typeof message.confidence === "number" && !message.isStreaming && (
            <ConfidenceBar confidence={message.confidence} />
          )}

          {hasSources && (
            <div className="mt-3">
              <button
                onClick={() => setSourcesOpen((o) => !o)}
                className="flex items-center gap-1 text-xs font-medium text-text-secondary transition-colors hover:text-text-primary"
              >
                {sourcesOpen ? (
                  <ChevronDown size={14} />
                ) : (
                  <ChevronRight size={14} />
                )}
                Извори ({message.sources!.length})
              </button>
              {sourcesOpen && (
                <div className="mt-2 space-y-2">
                  {message.sources!.map((s, i) => (
                    <SourceCard key={i} source={s} />
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        {!message.isStreaming && (
          <div className="mt-2 flex items-center gap-3 pl-1">
            <button
              onClick={copy}
              className="flex items-center gap-1 text-xs text-text-secondary transition-colors hover:text-text-primary"
            >
              {copied ? <Check size={14} /> : <Copy size={14} />}
              {copied ? "Копирано" : "Копирај"}
            </button>
          </div>
        )}

        {message.followUps && message.followUps.length > 0 && !message.isStreaming && (
          <div className="mt-3 space-y-1.5">
            <div className="flex items-center gap-1 text-xs text-text-secondary">
              <Sparkles size={12} /> Предложени прашања
            </div>
            {message.followUps.map((q, i) => (
              <button
                key={i}
                onClick={() => onFollowUp(q)}
                className="block w-full rounded-lg border border-border bg-bg/40 px-3 py-2 text-left text-sm text-text-secondary transition-colors hover:border-accent hover:text-text-primary"
              >
                {q}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};

export default ChatMessage;
