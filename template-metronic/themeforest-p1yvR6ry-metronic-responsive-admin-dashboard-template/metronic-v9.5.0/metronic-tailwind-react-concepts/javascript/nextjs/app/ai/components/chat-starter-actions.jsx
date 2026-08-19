import { Card } from '@/components/ui/card';
import { CHAT_STARTER_PERSONAS } from '../mock/chat-starter';

export function ChatStarterActions({ onSelect }) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 w-full">
      {CHAT_STARTER_PERSONAS.slice(0, 4).map((persona) => {
        const Icon = persona.icon;
        return (
          <Card
            key={persona.id}
            className="flex flex-row items-center gap-3 p-3 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors border-dashed shadow-none"
            onClick={() => onSelect?.(persona)}
          >
            <div className="flex items-center justify-center size-9 shrink-0 bg-gray-100 dark:bg-gray-800 rounded-lg">
              <Icon className="size-4 text-gray-700 dark:text-white" />
            </div>
            <div className="flex flex-col gap-0.5 min-w-0">
              <h3 className="font-semibold text-sm text-gray-900 dark:text-white truncate">
                {persona.name}
              </h3>
              <p className="text-xs text-muted-foreground truncate">
                {persona.description}
              </p>
            </div>
          </Card>
        );
      })}
    </div>
  );
}
