export interface CompletedTask {
  id: string;
  title: string;
  completedAt: string;
  priority: 'low' | 'medium' | 'high';
  category?: string;
}
