'use client';

import * as React from 'react';
import { Plus, Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Kanban, KanbanBoard, KanbanOverlay } from '@/components/ui/kanban';
import { useLayout } from '@/components/layouts/todo/components/context';
import {
  Toolbar,
  ToolbarActions,
  ToolbarDescription,
  ToolbarHeading,
  ToolbarPageTitle,
} from '@/components/layouts/todo/components/toolbar';
import { ToolbarSearch } from '@/components/layouts/todo/components/toolbar-search';
import { initialTasks } from '@/app/todo/mock';
import { TaskColumn } from './task-column';

export default function AllTasksPage() {
  const { isMobile, isAsideOpen, asideToggle } = useLayout();
  const [columns, setColumns] = React.useState(initialTasks);

  const totalTasks = React.useMemo(
    () => Object.values(columns).reduce((acc, tasks) => acc + tasks.length, 0),
    [columns],
  );

  return (
    <div className="container-fluid py-5">
      <ToolbarSearch />
      <Toolbar>
        <ToolbarHeading>
          <ToolbarPageTitle>All Tasks</ToolbarPageTitle>
          <ToolbarDescription>{totalTasks} tasks in total</ToolbarDescription>
        </ToolbarHeading>
        <ToolbarActions>
          <Button variant="mono">
            <Plus className="size-4" />
            New Activity
          </Button>
          {!isMobile && !isAsideOpen && (
            <Button mode="icon" variant="outline" onClick={asideToggle}>
              <Sparkles className="size-4" />
            </Button>
          )}
        </ToolbarActions>
      </Toolbar>

      <Kanban
        value={columns}
        onValueChange={setColumns}
        getItemValue={(item) => item.id}
      >
        <KanbanBoard className="grid auto-rows-fr grid-cols-1 md:grid-cols-3 gap-4">
          {Object.entries(columns).map(([columnValue, tasks]) => (
            <TaskColumn key={columnValue} value={columnValue} tasks={tasks} />
          ))}
        </KanbanBoard>
        <KanbanOverlay>
          <div className="rounded-md bg-muted/60 size-full" />
        </KanbanOverlay>
      </Kanban>
    </div>
  );
}
