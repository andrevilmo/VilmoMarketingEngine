import type { LucideIcon } from 'lucide-react';

export interface SidebarTodoListProps {
  isCollapsed: boolean;
}

export type TodoListItem = {
  id: number;
  title: string;
  icon: LucideIcon;
  badge: "primary" | "success" | "warning" | "destructive" | "info" | "secondary";
  count: number;
  path: string;
};
