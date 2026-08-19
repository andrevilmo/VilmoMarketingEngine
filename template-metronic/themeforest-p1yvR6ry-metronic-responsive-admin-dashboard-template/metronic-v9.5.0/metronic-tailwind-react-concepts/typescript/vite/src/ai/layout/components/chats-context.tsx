import {
  createContext,
  ReactNode,
  useContext,
  useState,
} from 'react';
import { ChatThread } from '@/ai/types';
import { RECENT_CHATS } from '@/ai/mock';

interface ChatsContextValue {
  chats: ChatThread[];
  setChats: React.Dispatch<React.SetStateAction<ChatThread[]>>;
}

const ChatsContext = createContext<ChatsContextValue | undefined>(undefined);

interface ChatsProviderProps {
  children: ReactNode;
}

export function ChatsProvider({ children }: ChatsProviderProps) {
  const [chats, setChats] = useState<ChatThread[]>(RECENT_CHATS);

  return (
    <ChatsContext.Provider value={{ chats, setChats }}>
      {children}
    </ChatsContext.Provider>
  );
}

export const useChats = () => {
  const context = useContext(ChatsContext);
  if (!context) {
    throw new Error('useChats must be used within a ChatsProvider');
  }
  return context;
};
