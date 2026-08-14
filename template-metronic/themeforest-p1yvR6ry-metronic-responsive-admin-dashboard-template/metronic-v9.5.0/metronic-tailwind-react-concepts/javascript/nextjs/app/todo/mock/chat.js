import { Clock, Lightbulb, ListChecks, Zap } from 'lucide-react';

export const suggestions = [
  {
    id: '1',
    icon: ListChecks,
    label: 'Prioritize tasks',
    prompt:
      'Help me prioritize my tasks for today based on deadlines and importance',
  },
  {
    id: '2',
    icon: Lightbulb,
    label: 'Break down task',
    prompt: 'Break down my complex tasks into smaller, actionable steps',
  },
  {
    id: '3',
    icon: Clock,
    label: 'Time estimates',
    prompt: 'Estimate how long each of my pending tasks will take to complete',
  },
  {
    id: '4',
    icon: Zap,
    label: 'Quick wins',
    prompt: 'Identify quick wins I can complete in under 15 minutes',
  },
];

export const initialMessages = [
  {
    id: '1',
    role: 'assistant',
    content:
      "Hi! I'm your AI assistant for task management. I can help you prioritize tasks, estimate time, break down complex projects, and boost your productivity. What would you like help with?",
    timestamp: new Date(),
  },
];
