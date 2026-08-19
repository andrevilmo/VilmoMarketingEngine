import { Code, FileText, Image, Lightbulb } from 'lucide-react';

export const RECENT_CHATS = [
  {
    id: '1',
    title: 'Website Redesign Plan',
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
