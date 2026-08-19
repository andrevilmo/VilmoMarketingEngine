import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { ChevronRight } from "lucide-react";
import { SectionHeader } from "./section-header";
import { useRouter } from "next/navigation";
import { useChats } from "./chats-context";

interface PinnedChatsProps {
  selectedChat: string | null;
  onChatSelect: (chatId: string) => void;
}

export function PinnedChats({ selectedChat, onChatSelect }: PinnedChatsProps) {
  const router = useRouter();
  const { chats } = useChats();
  const pinnedChats = chats.filter(chat => chat.isPinned);


  if (pinnedChats.length === 0) {
    return null;
  }

  const handleChatClick = (chatId: string) => {
    onChatSelect(chatId);
    router.push(`/ai/chat?chatId=${chatId}`);
  };

  return (
    <div className="space-y-2">
      <SectionHeader label="Pinned" />
      {pinnedChats.map((chat) => {
        const isSelected = selectedChat === chat.id;
        return (
          <Button
            key={chat.id}
            variant="ghost"
            autoHeight
            onClick={() => handleChatClick(chat.id)}
            className={cn(
              "w-full justify-start p-2 rounded-lg group border border-dashed border-gray-300 dark:border-gray-700 bg-muted/80",
              isSelected && "bg-muted/60"
            )}
          >
            <div className="flex-1 min-w-0 text-start">
              <h4 className="text-sm font-medium mb-1">
                {chat.title}
              </h4>
              <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-muted-foreground text-xs">
                <Badge variant="success" appearance="outline" size="sm">
                  {chat.model}
                </Badge>
                <span className="text-muted-foreground/70">•</span>
                <span>{chat.messageCount ?? 0} msgs</span>
              </div>
            </div>
            <ChevronRight className="size-3.5 text-muted-foreground/60 group-hover:text-foreground transition-colors opacity-0 group-hover:opacity-80" />
          </Button>
        );
      })}
    </div>
  );
}
