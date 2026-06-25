import { useCallback, useRef, useState } from "react";
import type { Source, StreamEvent } from "../types";

export interface ChatStreamHandlers {
  onSources?: (sources: Source[], confidence: number) => void;
  onToken?: (accumulated: string) => void;
}

export interface ChatStreamResult {
  content: string;
  sources: Source[];
  confidence: number;
  followUps: string[];
  error: string | null;
}

interface HistoryTurn {
  role: string;
  content: string;
}

export function useStreamingChat() {
  const [isStreaming, setIsStreaming] = useState(false);
  const controllerRef = useRef<AbortController | null>(null);

  const cancel = useCallback(() => {
    controllerRef.current?.abort();
  }, []);

  const send = useCallback(
    async (
      question: string,
      history: HistoryTurn[],
      handlers?: ChatStreamHandlers
    ): Promise<ChatStreamResult> => {
      setIsStreaming(true);
      const controller = new AbortController();
      controllerRef.current = controller;

      let content = "";
      let sources: Source[] = [];
      let confidence = 0;
      let followUps: string[] = [];
      let error: string | null = null;

      try {
        const res = await fetch("/api/chat/ask/stream", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ question, conversationHistory: history }),
          signal: controller.signal,
        });

        if (!res.ok || !res.body) {
          throw new Error(`Server responded with ${res.status}`);
        }

        const reader = res.body.getReader();
        const decoder = new TextDecoder();
        let buffer = "";

        for (;;) {
          const { done, value } = await reader.read();
          if (done) break;
          buffer += decoder.decode(value, { stream: true });

          let boundary: number;
          while ((boundary = buffer.indexOf("\n\n")) !== -1) {
            const rawEvent = buffer.slice(0, boundary).trim();
            buffer = buffer.slice(boundary + 2);
            if (!rawEvent.startsWith("data:")) continue;

            let evt: StreamEvent;
            try {
              evt = JSON.parse(rawEvent.slice(5).trim());
            } catch {
              continue;
            }

            switch (evt.type) {
              case "sources":
                sources = evt.sources;
                confidence = evt.confidence;
                handlers?.onSources?.(sources, confidence);
                break;
              case "token":
                content += evt.content;
                handlers?.onToken?.(content);
                break;
              case "followups":
                followUps = evt.followUps;
                break;
              case "error":
                error = evt.message;
                break;
              case "done":
                break;
            }
          }
        }
      } catch (e) {
        const err = e as { name?: string; message?: string };
        if (err?.name !== "AbortError") {
          error = err?.message ?? "Unknown error";
        }
      } finally {
        setIsStreaming(false);
        controllerRef.current = null;
      }

      return { content, sources, confidence, followUps, error };
    },
    []
  );

  return { send, cancel, isStreaming };
}
