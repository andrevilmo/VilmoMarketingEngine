export interface PriorityTask {
  id: string;
  title: string;
  priority: 'low' | 'medium' | 'high';
  dueDate?: string;
  completed: boolean;
  category?: string;
}
