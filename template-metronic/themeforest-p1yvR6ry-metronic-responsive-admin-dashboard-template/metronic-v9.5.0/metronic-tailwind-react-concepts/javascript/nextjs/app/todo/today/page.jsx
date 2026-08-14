'use client';

import * as React from 'react';
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
import { initialTodayTasks } from '@/app/todo/mock';
import { HighPriorityCard, ProgressCard, StreakCard } from './stats-cards';
import { TaskList } from './task-list';

export default function TodayPage() {
  const { isMobile, isAsideOpen, asideToggle } = useLayout();
  const [tasks, setTasks] = React.useState(initialTodayTasks);

  const completedCount = tasks.filter((t) => t.completed).length;
  const totalCount = tasks.length;
  const progressPercent = Math.round((completedCount / totalCount) * 100);
  const highPriorityCount = tasks.filter(
    (t) => t.priority === 'high' && !t.completed,
  ).length;

  const toggleTask = (taskId) => {
    setTasks((prev) =>
      prev.map((task) =>
        task.id === taskId ? { ...task, completed: !task.completed } : task,
      ),
    );
  };

  return (
    <div className="container-fluid py-5">
      <ToolbarSearch />
      <Toolbar>
        <ToolbarHeading>
          <ToolbarPageTitle>Today Activities</ToolbarPageTitle>
          <ToolbarDescription>
            Manage your reminders, to do list, events, etc.
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
          <ProgressCard
            completedCount={completedCount}
            totalCount={totalCount}
            progressPercent={progressPercent}
          />

          <HighPriorityCard count={highPriorityCount} />
          <StreakCard days={5} />
        </div>

        <TaskList tasks={tasks} onToggleTask={toggleTask} />
      </div>
    </div>
  );
}
