export interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

export interface Suggestion {
  id: string;
  icon: React.ElementType;
  label: string;
  prompt: string;
}

