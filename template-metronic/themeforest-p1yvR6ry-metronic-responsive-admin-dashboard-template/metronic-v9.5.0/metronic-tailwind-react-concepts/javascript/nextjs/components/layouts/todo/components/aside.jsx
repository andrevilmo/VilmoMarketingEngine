import { useCallback, useEffect, useRef, useState } from 'react';
import { AlertCircle, Bot, Sparkles, X } from 'lucide-react';
import { toAbsoluteUrl } from '@/lib/helpers';
import { cn } from '@/lib/utils';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { initialMessages, suggestions } from '@/app/todo/mock';
import { sendMessageWithFallback } from '@/app/todo/services/ai';
import { AsideInput } from './aside-input';
import { useLayout } from './context';

export function Aside({ onClose }) {
  const { isMobile, isAsideOpen, asideToggle } = useLayout();
  const [messages, setMessages] = useState(initialMessages);
  const [inputValue, setInputValue] = useState('');
  const [isTyping, setIsTyping] = useState(false);
  const [streamingContent, setStreamingContent] = useState('');
  const [error, setError] = useState(null);
  const scrollRef = useRef(null);

  const handleClose = () => {
    if (isMobile && onClose) {
      onClose();
    } else {
      asideToggle();
    }
  };

  const scrollToBottom = () => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages, streamingContent]);

  const sendToAI = useCallback(
    async (userContent) => {
      setIsTyping(true);
      setStreamingContent('');
      setError(null);

      // Prepare chat history for AI context
      const chatHistory = messages
        .filter((m) => m.role === 'user' || m.role === 'assistant')
        .map((m) => ({
          role: m.role,
          content: m.content,
        }));

      // Add the new user message
      chatHistory.push({ role: 'user', content: userContent });

      try {
        let fullResponse = '';

        await sendMessageWithFallback(chatHistory, (chunk) => {
          fullResponse += chunk;
          setStreamingContent(fullResponse);
          scrollToBottom();
        });

        // Add the complete AI response to messages
        const aiMessage = {
          id: Date.now().toString(),
          role: 'assistant',
          content: fullResponse,
          timestamp: new Date(),
        };

        setMessages((prev) => [...prev, aiMessage]);
        setStreamingContent('');
      } catch (err) {
        const errorMessage =
          err instanceof Error ? err.message : 'An error occurred';
        setError(errorMessage);
        console.error('AI Error:', err);
      } finally {
        setIsTyping(false);
      }
    },
    [messages],
  );

  const handleSend = () => {
    if (!inputValue.trim() || isTyping) return;

    const userMessage = {
      id: Date.now().toString(),
      role: 'user',
      content: inputValue.trim(),
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMessage]);
    const content = inputValue.trim();
    setInputValue('');
    sendToAI(content);
  };

  const handleSuggestionClick = (suggestion) => {
    setInputValue(suggestion.prompt);
  };

  const formatTime = (date) => {
    return date.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  return (
    <div
      className={cn(
        'lg:fixed lg:z-10 lg:top-2.5 lg:bottom-2.5 lg:end-2.5 flex flex-col p-5 items-stretch shrink-0 w-full h-full lg:h-auto lg:bg-background lg:border lg:border-input lg:rounded-xl lg:shadow-xs transition-[width,opacity,transform] duration-300 overflow-hidden',
        !isMobile &&
          !isAsideOpen &&
          'lg:opacity-0 lg:translate-x-4 lg:pointer-events-none lg:shadow-none lg:border-transparent',
      )}
      style={
        !isMobile
          ? { width: isAsideOpen ? 'var(--aside-width)' : '0px' }
          : undefined
      }
    >
      {/* Header */}
      <div className="flex items-center justify-between pb-5 shrink-0">
        <h1 className="flex items-center gap-2 text-base font-medium">
          <Sparkles className="size-4" />
          AI Assistant
        </h1>
        <Button
          size="sm"
          variant="dim"
          className="-me-1.5"
          onClick={handleClose}
        >
          <X className="size-4" />
        </Button>
      </div>

      {/* Messages Area */}
      <ScrollArea
        className="flex-1 min-h-0 max-h-[calc(100vh-13rem)] lg:max-h-none"
        ref={scrollRef}
      >
        <div className="px-2 space-y-4">
          {messages.map((message) => (
            <div
              key={message.id}
              className={cn(
                'flex gap-3',
                message.role === 'user' ? 'flex-row-reverse' : 'flex-row',
              )}
            >
              {message.role === 'assistant' ? (
                <Avatar className="size-8 shrink-0">
                  <AvatarFallback className="text-xs bg-linear-to-br from-zinc-900 to-zinc-500 text-white border-0">
                    <Bot className="size-4" />
                  </AvatarFallback>
                </Avatar>
              ) : (
                <Avatar className="size-8 shrink-0">
                  <AvatarImage
                    src={toAbsoluteUrl('/media/avatars/300-2.png')}
                    alt="@reui"
                  />
                  <AvatarFallback>AD</AvatarFallback>
                </Avatar>
              )}

              <div
                className={cn(
                  'flex flex-col gap-1 max-w-[85%]',
                  message.role === 'user' ? 'items-end' : 'items-start',
                )}
              >
                <div
                  className={cn(
                    'px-3.5 py-2.5 rounded-2xl text-sm leading-relaxed',
                    message.role === 'assistant'
                      ? 'bg-muted/60 text-foreground rounded-tl-md'
                      : 'bg-zinc-900 text-white dark:bg-zinc-100 dark:text-zinc-900 rounded-tr-md',
                  )}
                >
                  {message.role === 'assistant' ? (
                    <div className="prose prose-sm dark:prose-invert prose-p:my-1 prose-headings:my-2 prose-ul:my-1 prose-li:my-0 prose-table:my-2 max-w-none">
                      {message.content}
                    </div>
                  ) : (
                    <p className="whitespace-pre-wrap">{message.content}</p>
                  )}
                </div>
                <span className="text-[10px] text-muted-foreground px-1">
                  {formatTime(message.timestamp)}
                </span>
              </div>
            </div>
          ))}

          {/* Streaming Response */}
          {streamingContent && (
            <div className="flex gap-3">
              <Avatar className="size-8 shrink-0">
                <AvatarFallback className="text-xs bg-linear-to-br from-zinc-900 to-zinc-500 text-white border-0">
                  <Bot className="size-4" />
                </AvatarFallback>
              </Avatar>
              <div className="flex flex-col gap-1 max-w-[85%] items-start">
                <div className="px-3.5 py-2.5 rounded-2xl rounded-tl-md bg-muted/60 text-foreground text-sm leading-relaxed">
                  <div className="prose prose-sm dark:prose-invert prose-p:my-1 prose-headings:my-2 prose-ul:my-1 prose-li:my-0 prose-table:my-2 max-w-none">
                    {streamingContent}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Typing Indicator */}
          {isTyping && !streamingContent && (
            <div className="flex gap-3">
              <Avatar className="size-8 shrink-0">
                <AvatarFallback className="bg-linear-to-br from-zinc-900 to-zinc-500 text-white text-xs border-0">
                  <Bot className="size-4" />
                </AvatarFallback>
              </Avatar>
              <div className="px-4 py-3 rounded-2xl rounded-tl-md bg-muted/60">
                <div className="flex items-center gap-1">
                  <span className="size-2 bg-muted-foreground/60 rounded-full animate-bounce [animation-delay:0ms]" />
                  <span className="size-2 bg-muted-foreground/60 rounded-full animate-bounce [animation-delay:150ms]" />
                  <span className="size-2 bg-muted-foreground/60 rounded-full animate-bounce [animation-delay:300ms]" />
                </div>
              </div>
            </div>
          )}

          {/* Error Display */}
          {error && (
            <div className="flex gap-3">
              <Avatar className="size-8 shrink-0">
                <AvatarFallback className="text-xs bg-red-500 text-white border-0">
                  <AlertCircle className="size-4" />
                </AvatarFallback>
              </Avatar>
              <div className="flex flex-col gap-1 max-w-[85%] items-start">
                <div className="px-3.5 py-2.5 rounded-2xl rounded-tl-md bg-red-500/10 text-red-600 dark:text-red-400 text-sm">
                  <p className="font-medium">Error</p>
                  <p className="text-xs mt-1">{error}</p>
                </div>
              </div>
            </div>
          )}
        </div>
      </ScrollArea>

      {/* Suggestions */}
      {messages.length <= 1 && (
        <div className="flex flex-wrap gap-2 shrink-0">
          {suggestions.map((suggestion) => (
            <Badge
              key={suggestion.id}
              variant="secondary"
              className="cursor-pointer"
              onClick={() => handleSuggestionClick(suggestion)}
            >
              <suggestion.icon className="size-4" />
              {suggestion.label}
            </Badge>
          ))}
        </div>
      )}

      {/* Input Area */}
      <div className="pt-5 shrink-0 mt-auto">
        <AsideInput
          message={inputValue}
          onMessageChange={setInputValue}
          onSend={handleSend}
          compact
        />
      </div>
    </div>
  );
}
