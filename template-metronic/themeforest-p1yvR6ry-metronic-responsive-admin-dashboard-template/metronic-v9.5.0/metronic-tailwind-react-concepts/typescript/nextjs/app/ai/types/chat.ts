import { LucideIcon } from "lucide-react";

export interface ChatThread {
  id: string;
  title: string;
  model: string;
  timestamp: string;
  isPinned?: boolean;
  isFavorite?: boolean;
  icon: LucideIcon;
  messageCount?: number;
}

export interface AIModel {
  id: string;
  name: string;
  icon: LucideIcon;
  color: string;
}

export interface Message {
  id: string;
  role: "user" | "assistant";
  content: string;
  timestamp?: string;
  isStreaming?: boolean;
}

export interface ModelOption {
  id: string;
  label: string;
  tagline: string;
  description: string;
  icon: LucideIcon;
  badge?: {
    label: string;
    variant?: "info" | "success" | "warning";
  };
}
