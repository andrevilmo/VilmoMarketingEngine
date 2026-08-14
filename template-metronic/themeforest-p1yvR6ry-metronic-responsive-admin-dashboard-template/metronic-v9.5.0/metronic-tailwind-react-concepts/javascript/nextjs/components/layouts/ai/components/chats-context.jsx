import { createContext, useContext, useState } from 'react';
import { RECENT_CHATS } from '../../../../app/ai/mock/chat-threads';

const ChatsContext = createContext(undefined);

export function ChatsProvider({ children }) {
  const [chats, setChats] = useState(RECENT_CHATS);

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
