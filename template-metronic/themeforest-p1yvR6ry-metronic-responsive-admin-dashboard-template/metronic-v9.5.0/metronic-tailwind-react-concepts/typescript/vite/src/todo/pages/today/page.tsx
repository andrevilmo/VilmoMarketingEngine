"use client"

import * as React from 'react';
import { Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Toolbar,
  ToolbarHeading,
  ToolbarPageTitle,
  ToolbarDescription,
  ToolbarActions,
} from "@/todo/layout/components/toolbar";
import { useLayout } from "@/todo/layout/components/context";
import { ToolbarSearch } from "@/todo/layout/components/toolbar-search";
import { initialTodayTasks } from "@/todo/mock";
import { TodayTask } from "@/todo/types";
import { TaskList } from "./task-list";
import { ProgressCard, HighPriorityCard, StreakCard } from "./stats-cards";

export function TodayPage() {
  const { isMobile, isAsideOpen, asideToggle } = useLayout();
  const [tasks, setTasks] = React.useState<TodayTask[]>(initialTodayTasks);

  const completedCount = tasks.filter(t => t.completed).length;
  const totalCount = tasks.length;
  const progressPercent = Math.round((completedCount / totalCount) * 100);
  const highPriorityCount = tasks.filter(t => t.priority === 'high' && !t.completed).length;

  const toggleTask = (taskId: string) => {
    setTasks(prev => prev.map(task => 
      task.id === taskId ? { ...task, completed: !task.completed } : task
    ));
  };

  return (
    <div className="container-fluid py-5">
      <ToolbarSearch />
      <Toolbar>
        <ToolbarHeading>
          <ToolbarPageTitle>Today Activities</ToolbarPageTitle>
          <ToolbarDescription>Manage your reminders, to do list, events, etc.</ToolbarDescription>
        </ToolbarHeading>
        <ToolbarActions>
          {!isMobile && !isAsideOpen && <Button mode="icon" variant="outline" onClick={asideToggle}><Sparkles className="size-4" /></Button>}
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
