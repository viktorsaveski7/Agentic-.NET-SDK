import { useEffect, useRef, useState } from "react";
import type { KeyboardEvent } from "react";
import { useOutletContext } from "react-router-dom";
import { Loader2, Scale, Send } from "lucide-react";
import ChatMessage from "../components/ChatMessage";
import type { ChatContextValue } from "../types";

const STARTERS = [
  "Што е договор за продажба?",
  "Кои се основните права на работниците?",
  "Како се поднесува тужба до граѓански суд?",
  "Што предвидува законот за заштита на потрошувачите?",
];

export default function ChatPage() {
  const { messages, isStreaming, sendMessage } =
    useOutletContext<ChatContextValue>();
  const [input, setInput] = useState("");
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const submit = (text?: string) => {
    const value = (text ?? input).trim();
    if (!value || isStreaming) return;
    sendMessage(value);
    setInput("");
  };

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      submit();
    }
  };

  const isEmpty = messages.length === 0;

  return (
    <div className="flex h-full flex-col">
      <header className="relative overflow-hidden border-b border-border">
        <div className="gradient-pulse animate-pulse-gradient" />
        <div className="relative flex items-center gap-3 px-6 py-4">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-accent/20 text-accent-hover">
            <Scale size={18} />
          </div>
          <div>
            <h1 className="text-sm font-semibold text-text-primary">
              Македонски правен асистент
            </h1>
            <p className="text-xs text-text-secondary">
              Одговори засновани на македонското законодавство
            </p>
          </div>
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto max-w-3xl px-4 py-6">
          {isEmpty ? (
            <div className="flex flex-col items-center justify-center py-16 text-center">
              <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-accent/15 text-accent-hover">
                <Scale size={26} />
              </div>
              <h2 className="mb-1 text-lg font-semibold text-text-primary">
                Поставете правно прашање
              </h2>
              <p className="mb-6 max-w-md text-sm text-text-secondary">
                LexAI пребарува низ македонските закони и одговара со цитирани
                извори.
              </p>
              <div className="grid w-full max-w-xl gap-2 sm:grid-cols-2">
                {STARTERS.map((s) => (
                  <button
                    key={s}
                    onClick={() => submit(s)}
                    className="rounded-xl border border-border bg-surface px-4 py-3 text-left text-sm text-text-secondary transition-colors hover:border-accent hover:text-text-primary"
                  >
                    {s}
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <div className="space-y-6">
              {messages.map((m) => (
                <div key={m.id} id={`msg-${m.id}`}>
                  <ChatMessage message={m} onFollowUp={(q) => submit(q)} />
                </div>
              ))}
            </div>
          )}
          <div ref={bottomRef} />
        </div>
      </div>

      <div className="border-t border-border bg-bg/80 px-4 py-4">
        <div className="mx-auto max-w-3xl">
          <div className="flex items-end gap-2 rounded-2xl border border-border bg-surface px-3 py-2 focus-within:border-accent">
            <textarea
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={onKeyDown}
              rows={1}
              placeholder="Напишете прашање на македонски…"
              className="max-h-40 min-h-[24px] flex-1 resize-none bg-transparent py-1.5 text-sm text-text-primary outline-none placeholder:text-text-secondary"
            />
            <button
              onClick={() => submit()}
              disabled={isStreaming || input.trim().length === 0}
              className="flex h-9 w-9 flex-none items-center justify-center rounded-lg bg-accent text-white transition-colors hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-40"
            >
              {isStreaming ? (
                <Loader2 size={16} className="animate-spin" />
              ) : (
                <Send size={16} />
              )}
            </button>
          </div>
          <p className="mt-1.5 px-1 text-xs text-text-secondary">
            Enter за испраќање · Shift+Enter за нов ред
          </p>
        </div>
      </div>
    </div>
  );
}
