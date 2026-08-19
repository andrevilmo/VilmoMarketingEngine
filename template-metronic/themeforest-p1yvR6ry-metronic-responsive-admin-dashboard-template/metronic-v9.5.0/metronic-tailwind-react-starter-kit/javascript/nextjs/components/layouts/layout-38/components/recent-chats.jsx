import {
  Code,
  Copy,
  FileText,
  Image,
  Lightbulb,
  MoreVertical,
  Pin,
  Star,
  Trash2,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { SectionHeader } from './section-header';

// Static array of recent chats with manually assigned icons
export const RECENT_CHATS = [
  {
    id: '1',
    title: 'Musk threatens iOS devices ban',
    model: 'GPT-4',
    timestamp: '2 min ago',
    messageCount: 10,
    icon: FileText,
  },
  {
    id: '2',
    title: 'What to wear kayaking',
    model: 'Claude',
    timestamp: '1 hour ago',
    messageCount: 45,
    icon: Lightbulb,
  },
  {
    id: '3',
    title: 'Redux code example',
    model: 'GPT-4',
    timestamp: '3 hours ago',
    messageCount: 23,
    icon: Code,
  },
  {
    id: '4',
    title: 'Basketball player image',
    model: 'GPT-4',
    timestamp: '5 hours ago',
    messageCount: 12,
    icon: Image,
  },
  {
    id: '5',
    title: 'React JS code library',
    model: 'Claude',
    timestamp: 'Yesterday',
    icon: Code,
    messageCount: 10,
    isPinned: true,
  },
  {
    id: '6',
    title: 'Plan for travel in Barcelona',
    model: 'GPT-4',
    timestamp: 'Yesterday',
    messageCount: 15,
    icon: Lightbulb,
  },
];

function ChatItem({ chat, isSelected, onSelect, onDelete }) {
  const Icon = chat.icon;

  return (
    <div
      className={cn(
        'group relative flex items-center rounded-md hover:bg-muted px-2 py-1 has-[[data-state=open]]:bg-muted',
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
        <Icon className="size-4 flex-shrink-0 text-muted-foreground/60" />
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
          <DropdownMenuItem>
            <Star className="size-4" />
            <span>Add to Favorites</span>
          </DropdownMenuItem>
          <DropdownMenuItem>
            <Pin className="size-4" />
            <span>Pin Chat</span>
          </DropdownMenuItem>
          <DropdownMenuItem>
            <Copy className="size-4" />
            <span>Copy Link</span>
          </DropdownMenuItem>
          {onDelete && (
            <>
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
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}

export function RecentChats({ selectedChat, onChatSelect, onChatDelete }) {
  return (
    <div className="space-y-2">
      <SectionHeader label="Recent" />
      <div className="space-y-0.5">
        {RECENT_CHATS.map((chat) => (
          <ChatItem
            key={chat.id}
            chat={chat}
            isSelected={selectedChat === chat.id}
            onSelect={() => onChatSelect(chat.id)}
            onDelete={onChatDelete ? () => onChatDelete(chat.id) : undefined}
          />
        ))}
      </div>
    </div>
  );
}
