'use client';

import { useState } from 'react';
import { cn } from '@/lib/utils';
import { CHAT_STARTER_MODEL_OPTIONS } from '../mock/chat-starter';
import { ChatStarterActions } from './chat-starter-actions';
import { ChatStarterDisclaimer } from './chat-starter-disclaimer';
import { ChatStarterHeader } from './chat-starter-header';
import { ChatStarterInput } from './chat-starter-input';

export function ChatStarter({
  onSend,
  selectedModelId = 'expert',
  onModelChange,
  onPersonaSelect,
  className,
  compact = false,
}) {
  const [message, setMessage] = useState('');
  const selectedModel =
    CHAT_STARTER_MODEL_OPTIONS.find((m) => m.id === selectedModelId) ??
    CHAT_STARTER_MODEL_OPTIONS[2];
  const handleSend = () => {
    if (!message.trim()) return;
    onSend?.(message.trim());
    setMessage('');
  };

  return (
    <div
      className={cn(
        compact
          ? 'flex flex-col w-full'
          : 'flex flex-col items-center justify-center flex-1 p-6',
        className,
      )}
    >
      {!compact && <ChatStarterHeader />}
      <div className={cn('w-full', !compact && 'max-w-3xl')}>
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
