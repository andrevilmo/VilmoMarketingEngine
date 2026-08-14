'use client';

import { cn } from '@/lib/utils';
import { ChatMessage } from './chat-message';

export function ChatMessages({ messages = [], className, children }) {
  return (
    <div
      className={cn(
        'flex flex-col h-full overflow-y-auto space-y-3.5',
        className,
      )}
    >
      {messages.map((message) => (
        <ChatMessage
          key={message.id}
          role={message.role}
          content={message.content}
          timestamp={message.timestamp}
          isStreaming={message.isStreaming}
        />
      ))}
      {children}
    </div>
  );
}
