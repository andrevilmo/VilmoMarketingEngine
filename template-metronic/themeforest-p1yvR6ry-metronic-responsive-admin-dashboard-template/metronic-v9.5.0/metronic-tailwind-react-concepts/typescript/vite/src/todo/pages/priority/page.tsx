"use client"

import { useState } from "react";
import { Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Toolbar,
  ToolbarHeading,
  ToolbarPageTitle,
  ToolbarDescription,
  ToolbarActions,
} from "@/todo/layout/components/toolbar";
import { ToolbarSearch } from "@/todo/layout/components/toolbar-search";
import { useLayout } from "@/todo/layout/components/context";
import { initialPriorityTasks } from "@/todo/mock";
import { PriorityTask } from "@/todo/types";
import { TaskList } from "./task-list";
import { HighPriorityCard, MediumPriorityCard, LowPriorityCard } from "./stats-cards";

export function PriorityPage() {
  const { isMobile, isAsideOpen, asideToggle } = useLayout();
  const [tasks, setTasks] = useState<PriorityTask[]>(initialPriorityTasks);

  const handleToggleTask = (taskId: string) => {
    setTasks(prev =>
      prev.map(task =>
        task.id === taskId ? { ...task, completed: !task.completed } : task
      )
    );
  };

  const pendingTasks = tasks.filter(t => !t.completed);
  const highCount = pendingTasks.filter(t => t.priority === 'high').length;
  const mediumCount = pendingTasks.filter(t => t.priority === 'medium').length;
  const lowCount = pendingTasks.filter(t => t.priority === 'low').length;

  return (
    <div className="container-fluid py-5">
      <ToolbarSearch />
      <Toolbar>
        <ToolbarHeading>
          <ToolbarPageTitle>Priority Tasks</ToolbarPageTitle>
          <ToolbarDescription>{pendingTasks.length} tasks pending</ToolbarDescription>
        </ToolbarHeading>
        <ToolbarActions>
          {!isMobile && !isAsideOpen && <Button mode="icon" variant="outline" onClick={asideToggle}><Sparkles className="size-4" /></Button>}
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
