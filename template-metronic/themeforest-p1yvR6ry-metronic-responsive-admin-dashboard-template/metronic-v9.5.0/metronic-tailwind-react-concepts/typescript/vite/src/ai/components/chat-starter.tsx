"use client"

import { useState } from "react";
import { cn } from "@/lib/utils";
import { ChatStarterProps } from "@/ai/types";
import { CHAT_STARTER_MODEL_OPTIONS } from "@/ai/mock";
import { ChatStarterHeader } from "./chat-starter-header";
import { ChatStarterInput } from "./chat-starter-input";
import { ChatStarterActions } from "./chat-starter-actions";
import { ChatStarterDisclaimer } from "./chat-starter-disclaimer";

export function ChatStarter({
  onSend,
  selectedModelId = "expert",
  onModelChange,
  onPersonaSelect,
  className,
  compact = false,
}: ChatStarterProps) {
  const [message, setMessage] = useState("");
  const selectedModel =
    CHAT_STARTER_MODEL_OPTIONS.find((m: { id: string }) => m.id === selectedModelId) ??
    CHAT_STARTER_MODEL_OPTIONS[2];
  const handleSend = () => {
    if (!message.trim()) return;
    onSend?.(message.trim());
    setMessage("");
  };

  return (
    <div
      className={cn(
        compact
          ? "flex flex-col w-full"
          : "flex flex-col items-center justify-center flex-1 p-6",
        className,
      )}
    >
      {!compact && <ChatStarterHeader />}
      <div className={cn("w-full", !compact && "max-w-3xl")}>
        <ChatStarterInput
          message={message}
          onMessageChange={setMessage}
          onSend={handleSend}
          selectedModel={selectedModel}
          selectedModelId={selectedModelId}
          onModelChange={onModelChange}
          compact={compact}
        />

        {!compact && <ChatStarterActions onSelect={onPersonaSelect} />}

        {!compact && <ChatStarterDisclaimer />}
      </div>
    </div>
  );
}
