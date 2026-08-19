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
} from "@/components/layouts/todo/components/toolbar";
import { ToolbarSearch } from "@/components/layouts/todo/components/toolbar-search";
import { useLayout } from "@/components/layouts/todo/components/context";
import { TaskList } from "./task-list";
import { initialUpcomingTasks } from "@/app/todo/mock";
import { UpcomingTask } from "@/app/todo/types";
import { ScheduledCard, HighPriorityCard, TomorrowCard } from "./stats-cards";

export default function UpcomingPage() {
  const { isMobile, isAsideOpen, asideToggle } = useLayout();
  const [tasks, setTasks] = useState<UpcomingTask[]>(initialUpcomingTasks);

  const handleToggleTask = (taskId: string) => {
    setTasks(prev =>
      prev.map(task =>
        task.id === taskId ? { ...task, completed: !task.completed } : task
      )
    );
  };

  const pendingCount = tasks.filter(t => !t.completed).length;
  const highPriorityCount = tasks.filter(t => t.priority === 'high' && !t.completed).length;
  const tomorrowCount = tasks.filter(t => t.date === 'Tomorrow' && !t.completed).length;

  return (
    <div className="container-fluid py-5">
      <ToolbarSearch />
      <Toolbar>
        <ToolbarHeading>
          <ToolbarPageTitle>Upcoming Tasks</ToolbarPageTitle>
          <ToolbarDescription>{pendingCount} tasks scheduled</ToolbarDescription>
        </ToolbarHeading>
        <ToolbarActions>
          {!isMobile && !isAsideOpen && <Button mode="icon" variant="outline" onClick={asideToggle}><Sparkles className="size-4" /></Button>}
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
