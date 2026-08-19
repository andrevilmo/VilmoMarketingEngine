import {
  CalendarCheck,
  CheckCircle2,
  Clock3,
  Flag,
  ListTodo,
} from 'lucide-react';

// Todo lists for sidebar
export const todoLists = [
  {
    id: 1,
    title: 'All Tasks',
    icon: ListTodo,
    badge: 'success',
    count: 19,
    path: '/todo/all-tasks',
  },
  {
    id: 2,
    title: 'Today',
    icon: CalendarCheck,
    badge: 'primary',
    count: 7,
    path: '/todo/today',
  },
  {
    id: 3,
    title: 'Upcoming',
    icon: Clock3,
    badge: 'warning',
    count: 4,
    path: '/todo/upcoming',
  },
  {
    id: 4,
    title: 'Priority',
    icon: Flag,
    badge: 'destructive',
    count: 8,
    path: '/todo/priority',
  },
  {
    id: 5,
    title: 'Completed',
    icon: CheckCircle2,
    badge: 'info',
    count: 18,
    path: '/todo/completed',
  },
];
