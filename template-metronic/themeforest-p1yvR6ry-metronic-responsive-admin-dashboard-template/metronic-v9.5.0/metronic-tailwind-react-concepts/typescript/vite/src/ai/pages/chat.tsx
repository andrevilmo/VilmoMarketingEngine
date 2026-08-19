"use client";

import { useState, useEffect } from "react";
import { useSearchParams } from "react-router";
import { Toolbar, ToolbarHeading, ToolbarActions } from "@/ai/layout/components/toolbar";
import { ShareDialog } from "@/ai/layout/components/share-dialog";
import { ChatMessages } from "@/ai/components/chat-messages";
import { ChatStarter } from "@/ai/components/chat-starter";
import { getMessagesForChat, createInitialMessages } from "@/ai/mock/messages";
import { Message } from "@/ai/types";

export function AIChatPage() {
  const [searchParams] = useSearchParams();
  const chatId = searchParams.get("chatId");
  const [selectedModelId, setSelectedModelId] = useState<string>("expert");
  const [messages, setMessages] = useState<Message[]>([]);

  useEffect(() => {
    if (chatId) {
      setMessages(getMessagesForChat(chatId));
    } else {
      setMessages(createInitialMessages());
    }
  }, [chatId]);

  const handleSendMessage = (content: string) => {
    if (!content.trim()) return;

    const userMessage: Message = {
      id: `user-${Date.now()}`,
      role: "user",
      content: content.trim(),
      timestamp: new Date().toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
      }),
    };

    setMessages((prev) => [...prev, userMessage]);

    // Simulate AI response
    setTimeout(() => {
      const aiMessage: Message = {
        id: `assistant-${Date.now()}`,
        role: "assistant",
        content: "This is a simulated response. In a real app, this would come from an API.",
        timestamp: new Date().toLocaleTimeString([], {
          hour: "2-digit",
          minute: "2-digit",
        }),
      };
      setMessages((prev) => [...prev, aiMessage]);
    }, 1000);
  };

  return (
    <div className="container-fluid flex flex-col h-[calc(100vh-var(--header-height-mobile)-1.25rem)] lg:h-[calc(100vh-2.2rem)]">
      <Toolbar>
        <div className="flex items-center gap-3">
          <ToolbarHeading>
            <div className="text-lg font-semibold">Chat</div>
          </ToolbarHeading>
        </div>

        <ToolbarActions>
          <ShareDialog />
        </ToolbarActions>
      </Toolbar>

      <div className="flex-1 flex flex-col rounded-lg bg-background overflow-hidden">
        {messages.length === 0 ? (
          <ChatStarter
            onSend={handleSendMessage}
            selectedModelId={selectedModelId}
            onModelChange={setSelectedModelId}
            compact={false}
          />
        ) : (
          <>
            <ChatMessages
              messages={messages}
              className="flex-1 overflow-y-auto px-6 py-4"
            />
            <div className="p-4 pb-6">
              <div className="max-w-3xl mx-auto w-full">
                <ChatStarter
                  onSend={handleSendMessage}
                  selectedModelId={selectedModelId}
                  onModelChange={setSelectedModelId}
                  compact={true}
                  className="p-0"
                />
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
