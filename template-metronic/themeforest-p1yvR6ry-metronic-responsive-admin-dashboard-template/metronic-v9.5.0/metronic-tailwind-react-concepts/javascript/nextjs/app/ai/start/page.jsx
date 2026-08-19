'use client';

import { useState } from 'react';
import { ShareDialog } from '@/components/layouts/ai/components/share-dialog';
import {
  Toolbar,
  ToolbarActions,
  ToolbarHeading,
} from '@/components/layouts/ai/components/toolbar';
import { ChatMessages } from '../components/chat-messages';
import { ChatStarter } from '../components/chat-starter';
import { DEFAULT_ASSISTANT_RESPONSE } from '../mock/messages';

export default function AIStartPage() {
  const [selectedModelId, setSelectedModelId] = useState('expert');
  const [messages, setMessages] = useState([]);

  const handleSendMessage = (content) => {
    if (!content.trim()) return;

    const userMessage = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: content.trim(),
      timestamp: new Date().toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
      }),
    };

    setMessages((prev) => [...prev, userMessage]);

    setTimeout(() => {
      const aiMessage = {
        id: `assistant-${Date.now()}`,
        role: 'assistant',
        content: DEFAULT_ASSISTANT_RESPONSE,
        timestamp: new Date().toLocaleTimeString([], {
          hour: '2-digit',
          minute: '2-digit',
        }),
      };
      setMessages((prev) => [...prev, aiMessage]);
    }, 1000);
  };

  const handlePersonaSelect = (persona) => {
    handleSendMessage(`Act as a ${persona.name}. ${persona.description}`);
  };

  return (
    <div className="container-fluid flex flex-col h-[calc(100vh-var(--header-height-mobile)--25rem)] lg:h-[calc(100vh-2.5rem)]">
      <Toolbar>
        <div className="flex items-center gap-3">
          <ToolbarHeading>
            <div className="text-lg font-semibold">New Chat</div>
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
            onPersonaSelect={handlePersonaSelect}
            compact={false}
          />
        ) : (
          <>
            <ChatMessages
              messages={messages}
              className="flex-1 overflow-y-auto px-6 py-4 scroll-smooth"
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
