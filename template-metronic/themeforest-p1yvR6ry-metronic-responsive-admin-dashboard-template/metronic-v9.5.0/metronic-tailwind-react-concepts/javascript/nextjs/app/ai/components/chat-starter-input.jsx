import * as React from 'react';
import { Check, ChevronDown, Lock, Mic, Paperclip } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { CHAT_STARTER_MODEL_OPTIONS } from '../mock/chat-starter';

export function ChatStarterInput({
  message,
  onMessageChange,
  onSend,
  selectedModel,
  selectedModelId,
  onModelChange,
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
          'relative flex flex-col gap-2 bg-background transition-all rounded-2xl border shadow-lg p-4',
        )}
      >
        <Input
          type="text"
          value={message}
          onChange={(e) => onMessageChange(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Message..."
          className="flex-1 border-0 bg-transparent shadow-none focus-visible:ring-0 placeholder:text-muted-foreground h-auto px-0 text-sm py-2"
        />

        <div className="flex items-center justify-between mt-2">
          <div className="flex items-center gap-2">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="outline"
                  size="sm"
                  className="h-8 px-3 rounded-lg bg-muted/50 hover:bg-muted text-xs font-medium gap-1.5 border-0"
                >
                  {React.createElement(selectedModel.icon, {
                    className: 'size-3.5',
                  })}
                  <span>{selectedModel.name}</span>
                  <ChevronDown className="size-3 opacity-50" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" className="w-64">
                {CHAT_STARTER_MODEL_OPTIONS.map((model) => {
                  const Icon = model.icon;
                  const isSelected = model.id === selectedModelId;
                  return (
                    <DropdownMenuItem
                      key={model.id}
                      onClick={() => !model.locked && onModelChange?.(model.id)}
                      className={cn(
                        'flex items-center gap-3 px-3 py-2.5 cursor-pointer',
                        model.locked && 'opacity-50 cursor-not-allowed',
                      )}
                    >
                      {React.createElement(Icon, {
                        className: 'size-4 shrink-0',
                      })}
                      <div className="flex-1 min-w-0">
                        <div className="font-medium text-sm">{model.name}</div>
                        {model.description && (
                          <div className="text-xs text-muted-foreground truncate">
                            {model.description}
                          </div>
                        )}
                      </div>
                      <div className="flex items-center gap-2 shrink-0">
                        {model.upgrade && (
                          <Button
                            variant="mono"
                            size="sm"
                            className="h-5 px-2 text-xs"
                            onClick={(e) => {
                              e.stopPropagation();
                            }}
                          >
                            Upgrade
                          </Button>
                        )}
                        {model.customize && (
                          <Button
                            variant="outline"
                            size="sm"
                            className="h-5 px-2 text-xs"
                            onClick={(e) => {
                              e.stopPropagation();
                            }}
                          >
                            Customize
                          </Button>
                        )}
                        {model.locked && (
                          <Lock className="size-3.5 text-muted-foreground" />
                        )}
                        {isSelected && !model.locked && (
                          <Check className="size-4 text-muted-foreground" />
                        )}
                      </div>
                    </DropdownMenuItem>
                  );
                })}
              </DropdownMenuContent>
            </DropdownMenu>

            <Button
              variant="ghost"
              size="icon"
              className="size-8 text-muted-foreground hover:text-foreground rounded-lg"
            >
              <Paperclip className="size-4" />
            </Button>
          </div>

          <div className="flex items-center gap-2">
            <Button
              variant={message.trim() ? 'primary' : 'secondary'}
              size="icon"
              className={cn(
                'size-9 rounded-xl transition-all',
                message.trim() ? 'opacity-100' : 'opacity-50',
              )}
              onClick={onSend}
              disabled={!message.trim()}
            >
              <Mic className="size-4" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
