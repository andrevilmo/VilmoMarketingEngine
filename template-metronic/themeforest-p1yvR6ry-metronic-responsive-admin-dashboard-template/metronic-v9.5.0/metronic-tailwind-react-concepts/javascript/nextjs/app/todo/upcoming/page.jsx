'use client';

import { useState } from 'react';
import { Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useLayout } from '@/components/layouts/todo/components/context';
import {
  Toolbar,
  ToolbarActions,
  ToolbarDescription,
  ToolbarHeading,
  ToolbarPageTitle,
} from '@/components/layouts/todo/components/toolbar';
import { ToolbarSearch } from '@/components/layouts/todo/components/toolbar-search';
import { initialUpcomingTasks } from '@/app/todo/mock';
import { HighPriorityCard, ScheduledCard, TomorrowCard } from './stats-cards';
import { TaskList } from './task-list';

export default function UpcomingPage() {
  const { isMobile, isAsideOpen, asideToggle } = useLayout();
  const [tasks, setTasks] = useState(initialUpcomingTasks);

  const handleToggleTask = (taskId) => {
    setTasks((prev) =>
      prev.map((task) =>
        task.id === taskId ? { ...task, completed: !task.completed } : task,
      ),
    );
  };

  const pendingCount = tasks.filter((t) => !t.completed).length;
  const highPriorityCount = tasks.filter(
    (t) => t.priority === 'high' && !t.completed,
  ).length;
  const tomorrowCount = tasks.filter(
    (t) => t.date === 'Tomorrow' && !t.completed,
  ).length;

  return (
    <div className="container-fluid py-5">
      <ToolbarSearch />
      <Toolbar>
        <ToolbarHeading>
          <ToolbarPageTitle>Upcoming Tasks</ToolbarPageTitle>
          <ToolbarDescription>
            {pendingCount} tasks scheduled
          </ToolbarDescription>
        </ToolbarHeading>
        <ToolbarActions>
          {!isMobile && !isAsideOpen && (
            <Button mode="icon" variant="outline" onClick={asideToggle}>
              <Sparkles className="size-4" />
            </Button>
          )}
        </ToolbarActions>
      </Toolbar>

      <div className="flex flex-col gap-5">
        {/* Stats Row */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
          <ScheduledCard count={pendingCount} />
          <HighPriorityCard count={highPriorityCount} />
          <TomorrowCard count={tomorrowCount} />
        </div>

        <TaskList tasks={tasks} onToggleTask={handleToggleTask} />
      </div>
    </div>
  );
}
