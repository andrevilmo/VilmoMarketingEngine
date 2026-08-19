import { createContext, useCallback, useContext, useState } from 'react';

const NewChatContext = createContext(undefined);

export function NewChatProvider({ children }) {
  const [requestId, setRequestId] = useState(0);

  const startNewChat = useCallback(() => {
    setRequestId((prev) => prev + 1);
  }, []);

  return (
    <NewChatContext.Provider value={{ requestId, startNewChat }}>
      {children}
    </NewChatContext.Provider>
  );
}

export function useNewChat() {
  const context = useContext(NewChatContext);
  if (!context) {
    throw new Error('useNewChat must be used within a NewChatProvider');
  }
  return context;
}
