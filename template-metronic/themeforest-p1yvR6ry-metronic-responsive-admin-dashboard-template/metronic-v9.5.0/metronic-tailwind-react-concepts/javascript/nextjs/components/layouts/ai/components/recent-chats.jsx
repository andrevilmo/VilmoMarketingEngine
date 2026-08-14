import { useRouter } from 'next/navigation';
import { Copy, MoreVertical, Pin, PinOff, Star, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useChats } from './chats-context';
import { SectionHeader } from './section-header';

function ChatItem({
  chat,
  isSelected,
  onSelect,
  onDelete,
  onPin,
  onFavorite,
  onCopyLink,
}) {
  const Icon = chat.icon;
  const isPinned = chat.isPinned || false;
  const isFavorite = chat.isFavorite || false;

  return (
    <div
      className={cn(
        'group relative flex items-center rounded-md hover:bg-muted px-2 py-1 has-data-[state=open]:bg-muted',
        isSelected
          ? 'bg-primary/10 text-primary'
          : 'bg-background hover:bg-muted',
      )}
    >
      <Button
        variant="ghost"
        onClick={onSelect}
        className={cn(
          'bg-transparent! justify-start text-foreground/80 flex-1 truncate text-ellipsis w-[195px] p-0 h-auto text-xs',
        )}
      >
        <Icon className="size-4 shrink-0 text-muted-foreground/60" />
        <span className="text-sm font-medium truncate text-start">
          {chat.title}
        </span>
      </Button>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="icon"
            className="ms-auto opacity-0 group-hover:opacity-100 data-[state=open]:opacity-100 transition-opacity size-6 -me-1"
            onClick={(e) => e.stopPropagation()}
          >
            <MoreVertical className="size-3.5" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-48">
          {onFavorite && (
            <DropdownMenuItem
              onClick={(e) => {
                e.stopPropagation();
                onFavorite();
              }}
            >
              <Star
                className={cn(
                  'size-4',
                  isFavorite && 'fill-yellow-400 text-yellow-400',
                )}
              />
              <span>
                {isFavorite ? 'Remove from Favorites' : 'Add to Favorites'}
              </span>
            </DropdownMenuItem>
          )}
          {onPin && (
            <DropdownMenuItem
              onClick={(e) => {
                e.stopPropagation();
                onPin();
              }}
            >
              {isPinned ? (
                <>
                  <PinOff className="size-4" />
                  <span>Unpin Chat</span>
                </>
              ) : (
                <>
                  <Pin className="size-4" />
                  <span>Pin Chat</span>
                </>
              )}
            </DropdownMenuItem>
          )}
          {onCopyLink && (
            <DropdownMenuItem
              onClick={(e) => {
                e.stopPropagation();
                onCopyLink();
              }}
            >
              <Copy className="size-4" />
              <span>Copy Link</span>
            </DropdownMenuItem>
          )}
          {onDelete && (
            <DropdownMenuItem
              className="text-destructive focus:text-destructive"
              onClick={(e) => {
                e.stopPropagation();
                onDelete();
              }}
            >
              <Trash2 className="size-4" />
              <span>Delete</span>
            </DropdownMenuItem>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}

export function RecentChats({ selectedChat, onChatSelect, onChatDelete }) {
  const router = useRouter();
  const { chats, setChats } = useChats();

  const handleChatClick = (chatId) => {
    onChatSelect(chatId);
    router.push(`/ai/chat?chatId=${chatId}`);
  };

  const handlePinChat = (chatId) => {
    const chat = chats.find((c) => c.id === chatId);
    const wasPinned = chat?.isPinned || false;

    setChats((prevChats) =>
      prevChats.map((chat) =>
        chat.id === chatId ? { ...chat, isPinned: !chat.isPinned } : chat,
      ),
    );

    toast.success(
      wasPinned ? 'Chat unpinned successfully' : 'Chat pinned successfully',
    );
  };

  const handleFavoriteChat = (chatId) => {
    setChats((prevChats) => {
      const chat = prevChats.find((c) => c.id === chatId);
      const isFavorite = chat?.isFavorite || false;
      const updatedChats = prevChats.map((chat) =>
        chat.id === chatId ? { ...chat, isFavorite: !isFavorite } : chat,
      );
      toast.success(
        isFavorite ? 'Removed from favorites' : 'Added to favorites',
      );
      return updatedChats;
    });
  };

  const handleCopyLink = async () => {
    try {
      const chatUrl = `${window.location.origin}/ai/chat`;
      await navigator.clipboard.writeText(chatUrl);
      toast.success('Chat link copied to clipboard!');
    } catch (error) {
      toast.error('Failed to copy link. Please try again.');
      console.error('Failed to copy:', error);
    }
  };

  const handleDeleteChat = (chatId) => {
    setChats((prevChats) => prevChats.filter((chat) => chat.id !== chatId));
    if (onChatDelete) {
      onChatDelete(chatId);
    }
    toast.success('Chat deleted successfully');
  };

  return (
    <div className="space-y-2">
      <SectionHeader label="Recent" />
      <div className="space-y-0.5">
        {chats.map((chat) => (
          <ChatItem
            key={chat.id}
            chat={chat}
            isSelected={selectedChat === chat.id}
            onSelect={() => handleChatClick(chat.id)}
            onDelete={() => handleDeleteChat(chat.id)}
            onPin={() => handlePinChat(chat.id)}
            onFavorite={() => handleFavoriteChat(chat.id)}
            onCopyLink={() => handleCopyLink()}
          />
        ))}
      </div>
    </div>
  );
}
