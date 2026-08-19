'use client';

import {
  Bot,
  Copy,
  MoreHorizontal,
  RotateCcw,
  Share2,
  ThumbsDown,
  ThumbsUp,
} from 'lucide-react';
import { toast } from 'sonner';
import { toAbsoluteUrl } from '@/lib/helpers';
import { cn } from '@/lib/utils';
import {
  Avatar,
  AvatarFallback,
  AvatarImage,
  AvatarIndicator,
  AvatarStatus,
} from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';

export function ChatMessage({ role, content, timestamp, isStreaming }) {
  const isUser = role === 'user';

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(content);
      toast.success('Message copied to clipboard');
    } catch {
      toast.error('Failed to copy message');
    }
  };

  const handleThumbsUp = () => {
    toast.success('Feedback submitted');
  };

  const handleThumbsDown = () => {
    toast.success('Feedback submitted');
  };

  const handleShare = async () => {
    try {
      if (navigator.share) {
        await navigator.share({
          title: 'AI Response',
          text: content,
        });
      } else {
        await navigator.clipboard.writeText(content);
        toast.success('Message copied to clipboard');
      }
    } catch {
      // User cancelled or error occurred
    }
  };

  const handleRegenerate = () => {
    toast.info('Regenerating response...');
  };

  const handleMore = () => {
    toast.info('More options');
  };

  // Parse content into structured HTML elements
  const renderContent = () => {
    const lines = content.split('\n');
    const elements = [];
    let currentList = null;

    const flushList = () => {
      if (currentList) {
        if (currentList.type === 'ul') {
          elements.push(
            <ul key={`ul-${elements.length}`} className="my-3 space-y-1.5">
              {currentList.items.map((item, i) => (
                <li key={i} className="flex items-start gap-2 pl-1">
                  <span className="mt-2 size-1 rounded-full bg-current shrink-0 opacity-70" />
                  <span className="flex-1">{parseInline(item)}</span>
                </li>
              ))}
            </ul>,
          );
        } else {
          elements.push(
            <ol key={`ol-${elements.length}`} className="my-3 space-y-1.5">
              {currentList.items.map((item, i) => (
                <li key={i} className="flex items-start gap-2 pl-1">
                  <span className="font-medium text-muted-foreground text-sm shrink-0">
                    {i + 1}.
                  </span>
                  <span className="flex-1">{parseInline(item)}</span>
                </li>
              ))}
            </ol>,
          );
        }
        currentList = null;
      }
    };

    const parseInline = (text) => {
      // Handle bold **text**
      const parts = text.split(/(\*\*.*?\*\*)/g);
      return parts.map((part, i) => {
        if (part.startsWith('**') && part.endsWith('**')) {
          return (
            <strong key={i} className="font-semibold text-muted-foreground">
              {part.slice(2, -2)}
            </strong>
          );
        }
        return part;
      });
    };

    lines.forEach((line, index) => {
      const trimmed = line.trim();

      // Empty line
      if (!trimmed) {
        flushList();
        if (elements.length > 0) {
          elements.push(<div key={`space-${index}`} className="h-3" />);
        }
        return;
      }

      // Bullet list item
      if (trimmed.match(/^[•\-*\.]\s/)) {
        const content = trimmed.replace(/^[•\-*\.]\s*/, '');
        if (!currentList || currentList.type !== 'ul') {
          flushList();
          currentList = { type: 'ul', items: [] };
        }
        currentList.items.push(content);
        return;
      }

      // Numbered list item
      const numberMatch = trimmed.match(/^(\d+)\.\s+(.+)$/);
      if (numberMatch) {
        const content = numberMatch[2];
        if (!currentList || currentList.type !== 'ol') {
          flushList();
          currentList = { type: 'ol', items: [] };
        }
        currentList.items.push(content);
        return;
      }

      // Header (bold line or ### prefix)
      if (
        (trimmed.startsWith('**') && trimmed.endsWith('**')) ||
        trimmed.startsWith('###')
      ) {
        flushList();
        const headerText = trimmed
          .replace(/^###\s*/, '')
          .replace(/^\*\*|\*\*$/g, '');
        elements.push(
          <h3
            key={index}
            className="font-bold text-[15px] mt-5 mb-2.5 first:mt-0 text-muted-foreground"
          >
            {headerText}
          </h3>,
        );
        return;
      }

      // Regular paragraph
      flushList();
      elements.push(
        <p key={index} className="my-1 leading-relaxed">
          {parseInline(line)}
        </p>,
      );
    });

    flushList();
    return elements;
  };

  return (
    <div
      className={cn(
        'flex items-start gap-3 py-4',
        isUser && 'flex-row-reverse',
      )}
    >
      {isUser ? (
        <Avatar className="size-9">
          <AvatarImage
            src={toAbsoluteUrl('/media/avatars/300-2.png')}
            alt="@reui"
          />
          <AvatarFallback>AI</AvatarFallback>
          <AvatarIndicator className="-end-1.5 -top-1.5">
            <AvatarStatus variant="online" className="size-2.5" />
          </AvatarIndicator>
        </Avatar>
      ) : (
        <Avatar className="size-8 shrink-0">
          <AvatarFallback className="bg-primary/10">
            <Bot className="size-4 text-primary" />
          </AvatarFallback>
        </Avatar>
      )}

      <div className={cn('flex flex-col gap-1 flex-1', isUser && 'items-end')}>
        <div
          className={cn(
            'rounded-2xl px-5 py-3.5 text-sm shadow-sm relative group',
            isUser
              ? 'bg-primary text-primary-foreground max-w-[85%] rounded-br-sm'
              : 'bg-muted/50 text-foreground max-w-[90%] rounded-bl-sm',
          )}
        >
          <div className="text-sm">{renderContent()}</div>
          {isStreaming && !isUser && (
            <span className="inline-block w-2 h-4 ml-1 bg-current animate-pulse" />
          )}

          {/* Toolbar for AI messages */}
          {!isUser && !isStreaming && (
            <div className="flex items-center gap-1 mt-3 pt-3 border-t border-border">
              <Button
                variant="ghost"
                size="icon"
                className="size-7 h-7 text-muted-foreground hover:text-foreground hover:bg-zinc-100 dark:bg-zinc-800"
                onClick={handleCopy}
                title="Copy"
              >
                <Copy className="size-3.5" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="size-7 h-7 text-muted-foreground hover:text-foreground hover:bg-zinc-100 dark:bg-zinc-800"
                onClick={handleThumbsUp}
                title="Thumbs up"
              >
                <ThumbsUp className="size-3.5" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="size-7 h-7 text-muted-foreground hover:text-foreground hover:bg-zinc-100 dark:bg-zinc-800"
                onClick={handleThumbsDown}
                title="Thumbs down"
              >
                <ThumbsDown className="size-3.5" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="size-7 h-7 text-muted-foreground hover:text-foreground hover:bg-zinc-100 dark:bg-zinc-800"
                onClick={handleShare}
                title="Share"
              >
                <Share2 className="size-3.5" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="size-7 h-7 text-muted-foreground hover:text-foreground hover:bg-zinc-100 dark:bg-zinc-800"
                onClick={handleRegenerate}
                title="Regenerate"
              >
                <RotateCcw className="size-3.5" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="size-7 h-7 text-muted-foreground hover:text-foreground hover:bg-zinc-100 dark:bg-zinc-800"
                onClick={handleMore}
                title="More options"
              >
                <MoreHorizontal className="size-3.5" />
              </Button>
            </div>
          )}
        </div>
        {timestamp && (
          <span className="text-xs text-muted-foreground px-1">
            {timestamp}
          </span>
        )}
      </div>
    </div>
  );
}
