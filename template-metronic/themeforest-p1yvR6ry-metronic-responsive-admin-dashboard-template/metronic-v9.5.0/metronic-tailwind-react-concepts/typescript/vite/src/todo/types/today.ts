export interface TodayTask {
  id: string;
  title: string;
  time: string;
  priority: 'low' | 'medium' | 'high';
  completed: boolean;
}
