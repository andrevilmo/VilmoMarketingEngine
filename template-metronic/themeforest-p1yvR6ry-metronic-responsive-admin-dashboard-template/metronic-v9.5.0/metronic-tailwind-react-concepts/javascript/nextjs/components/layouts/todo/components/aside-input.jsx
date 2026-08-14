import * as React from 'react';
import { Paperclip, Send } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

export function AsideInput({
  message,
  onMessageChange,
  onSend,
  compact = false,
}) {
  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      onSend();
    }
  };

  return (
    <div className={cn('relative', !compact && 'mb-8')}>
      <div
        className={cn(
          'relative flex flex-col gap-2 bg-background transition-all rounded-2xl border shadow-sm p-3',
        )}
      >
        <Input
          type="text"
          value={message}
          onChange={(e) => onMessageChange(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Message..."
          className="flex-1 border-0 bg-transparent shadow-none focus-visible:ring-0 placeholder:text-muted-foreground h-auto px-0 text-sm"
        />

        <div className="flex items-center justify-between">
          <Button variant="ghost" mode="icon">
            <Paperclip className="size-4" />
          </Button>

          <Button
            variant={message.trim() ? 'mono' : 'ghost'}
            mode="icon"
            className={cn(
              'rounded-xl transition-all',
              message.trim() ? 'opacity-100' : 'opacity-50',
            )}
            onClick={onSend}
            disabled={!message.trim()}
          >
            <Send className="size-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
