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
import { initialPriorityTasks } from '@/app/todo/mock';
import {
  HighPriorityCard,
  LowPriorityCard,
  MediumPriorityCard,
} from './stats-cards';
import { TaskList } from './task-list';

export default function PriorityPage() {
  const { isMobile, isAsideOpen, asideToggle } = useLayout();
  const [tasks, setTasks] = useState(initialPriorityTasks);

  const handleToggleTask = (taskId) => {
    setTasks((prev) =>
      prev.map((task) =>
        task.id === taskId ? { ...task, completed: !task.completed } : task,
      ),
    );
  };

  const pendingTasks = tasks.filter((t) => !t.completed);
  const highCount = pendingTasks.filter((t) => t.priority === 'high').length;
  const mediumCount = pendingTasks.filter(
    (t) => t.priority === 'medium',
  ).length;
  const lowCount = pendingTasks.filter((t) => t.priority === 'low').length;

  return (
    <div className="container-fluid py-5">
      <ToolbarSearch />
      <Toolbar>
        <ToolbarHeading>
          <ToolbarPageTitle>Priority Tasks</ToolbarPageTitle>
          <ToolbarDescription>
            {pendingTasks.length} tasks pending
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
          <HighPriorityCard count={highCount} />
          <MediumPriorityCard count={mediumCount} />
          <LowPriorityCard count={lowCount} />
        </div>

        <TaskList tasks={tasks} onToggleTask={handleToggleTask} />
      </div>
    </div>
  );
}
