import { ReactNode } from "react";
import { LucideIcon } from "lucide-react";
import { Message } from "./chat";

export interface StarterModelOption {
  id: string;
  name: string;
  icon: LucideIcon;
  description: string;
  upgrade?: boolean;
  customize?: boolean;
  locked?: boolean;
}

export interface StarterPersona {
  id: string;
  name: string;
  icon: LucideIcon;
  description: string;
}

export interface ChatStarterProps {
  onSend?: (message: string) => void;
  selectedModelId?: string;
  onModelChange?: (modelId: string) => void;
  onPersonaSelect?: (persona: StarterPersona) => void;
  className?: string;
  compact?: boolean;
}

export interface ChatMessagesProps {
  messages?: Message[];
  className?: string;
  children?: ReactNode;
}

export interface ChatMessageProps {
  role: Message["role"];
  content: string;
  timestamp?: string;
  isStreaming?: boolean;
}
