'use client';

import { useState } from 'react';
import { useLayout } from '@/todo/layout/components/context';
import {
  Toolbar,
  ToolbarActions,
  ToolbarDescription,
  ToolbarHeading,
  ToolbarPageTitle,
} from '@/todo/layout/components/toolbar';
import { ToolbarSearch } from '@/todo/layout/components/toolbar-search';
import { initialCompletedTasks } from '@/todo/mock';
import { Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  ProductivityCard,
  ThisWeekCard,
  TotalCompletedCard,
} from './stats-cards';
import { TaskList } from './task-list';

export function CompletedPage() {
  const { isMobile, isAsideOpen, asideToggle } = useLayout();
  const [tasks, setTasks] = useState(initialCompletedTasks);

  const totalCount = tasks.length;
  const thisWeekCount = tasks.filter(
    (t) =>
      t.completedAt.startsWith('Today') ||
      t.completedAt.startsWith('Yesterday') ||
      t.completedAt.includes('Dec 16') ||
      t.completedAt.includes('Dec 17') ||
      t.completedAt.includes('Dec 18'),
  ).length;
  const productivityScore = 85;

  const handleDeleteTask = (taskId) => {
    setTasks((prev) => prev.filter((task) => task.id !== taskId));
  };

  return (
    <div className="container-fluid py-5">
      <ToolbarSearch />
      <Toolbar>
        <ToolbarHeading>
          <ToolbarPageTitle>Completed Tasks</ToolbarPageTitle>
          <ToolbarDescription>{totalCount} tasks completed</ToolbarDescription>
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
          <TotalCompletedCard count={totalCount} />
          <ThisWeekCard count={thisWeekCount} />
          <ProductivityCard percentage={productivityScore} />
        </div>

        <TaskList tasks={tasks} onDeleteTask={handleDeleteTask} />
      </div>
    </div>
  );
}
