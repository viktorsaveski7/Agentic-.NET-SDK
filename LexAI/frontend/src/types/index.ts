export interface Source {
  lawName: string;
  page: number;
  chunk: string;
  score: number;
}

export interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  sources?: Source[];
  confidence?: number;
  followUps?: string[];
  timestamp: Date;
  isStreaming?: boolean;
}

export interface ChatRequest {
  question: string;
  conversationHistory?: { role: string; content: string }[];
}

export interface IngestionResult {
  laws: number;
  chunks: number;
  lawNames: string[];
  log: string[];
}

export interface DocumentStats {
  laws: number;
  chunks: number;
  lawNames: string[];
}

export type StreamEvent =
  | { type: "sources"; sources: Source[]; confidence: number }
  | { type: "token"; content: string }
  | { type: "followups"; followUps: string[] }
  | { type: "done" }
  | { type: "error"; message: string };

export interface ChatContextValue {
  messages: ChatMessage[];
  isStreaming: boolean;
  sendMessage: (question: string) => void;
}
