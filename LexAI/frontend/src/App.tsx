import { useCallback, useEffect, useRef, useState } from "react";
import { Outlet, Route, Routes, useNavigate } from "react-router-dom";
import Sidebar from "./components/Sidebar";
import type { HistoryItem } from "./components/Sidebar";
import ChatPage from "./pages/ChatPage";
import SearchPage from "./pages/SearchPage";
import DocumentsPage from "./pages/DocumentsPage";
import { useStreamingChat } from "./hooks/useStreamingChat";
import type { ChatMessage as ChatMessageType } from "./types";

function genId(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function AppLayout() {
  const [messages, setMessages] = useState<ChatMessageType[]>([]);
  const { send, isStreaming } = useStreamingChat();
  const navigate = useNavigate();

  const messagesRef = useRef(messages);
  useEffect(() => {
    messagesRef.current = messages;
  }, [messages]);

  const updateMessage = useCallback(
    (id: string, updater: (m: ChatMessageType) => ChatMessageType) => {
      setMessages((prev) => prev.map((m) => (m.id === id ? updater(m) : m)));
    },
    []
  );

  const sendMessage = useCallback(
    async (question: string) => {
      const q = question.trim();
      if (!q || isStreaming) return;

      const history = messagesRef.current.map((m) => ({
        role: m.role,
        content: m.content,
      }));

      const userMsg: ChatMessageType = {
        id: genId(),
        role: "user",
        content: q,
        timestamp: new Date(),
      };
      const assistantId = genId();
      const assistantMsg: ChatMessageType = {
        id: assistantId,
        role: "assistant",
        content: "",
        timestamp: new Date(),
        isStreaming: true,
      };

      setMessages((prev) => [...prev, userMsg, assistantMsg]);

      const result = await send(q, history, {
        onSources: (sources, confidence) =>
          updateMessage(assistantId, (m) => ({ ...m, sources, confidence })),
        onToken: (acc) =>
          updateMessage(assistantId, (m) => ({ ...m, content: acc })),
      });

      updateMessage(assistantId, (m) => ({
        ...m,
        content: result.error
          ? m.content || `⚠️ ${result.error}`
          : result.content || m.content,
        sources: result.sources.length ? result.sources : m.sources,
        confidence: result.confidence || m.confidence,
        followUps: result.followUps,
        isStreaming: false,
      }));
    },
    [isStreaming, send, updateMessage]
  );

  const newChat = useCallback(() => setMessages([]), []);

  const selectHistory = useCallback(
    (id: string) => {
      navigate("/");
      window.setTimeout(() => {
        document
          .getElementById(`msg-${id}`)
          ?.scrollIntoView({ behavior: "smooth", block: "start" });
      }, 60);
    },
    [navigate]
  );

  const history: HistoryItem[] = messages
    .filter((m) => m.role === "user")
    .map((m) => ({
      id: m.id,
      title: m.content.length > 48 ? `${m.content.slice(0, 48)}…` : m.content,
    }));

  return (
    <div className="flex h-screen overflow-hidden bg-bg text-text-primary">
      <Sidebar
        history={history}
        onSelectHistory={selectHistory}
        onNewChat={newChat}
      />
      <main className="min-w-0 flex-1">
        <Outlet context={{ messages, isStreaming, sendMessage }} />
      </main>
    </div>
  );
}

export default function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<ChatPage />} />
        <Route path="search" element={<SearchPage />} />
        <Route path="documents" element={<DocumentsPage />} />
      </Route>
    </Routes>
  );
}
