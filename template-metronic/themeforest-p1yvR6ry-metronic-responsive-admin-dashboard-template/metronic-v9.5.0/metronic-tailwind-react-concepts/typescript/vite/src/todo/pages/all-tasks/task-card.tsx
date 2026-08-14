"use client"

import * as React from 'react';
import { Calendar } from "lucide-react";
import { Badge, BadgeDot } from "@/components/ui/badge";
import { KanbanItem, KanbanItemHandle } from "@/components/ui/kanban";
import { Task } from "@/todo/types";
import { cn } from "@/lib/utils";

function getPriorityDotColor(priority: string) {
  switch (priority) {
    case 'high': return 'bg-red-500';
    case 'medium': return 'bg-blue-500';
    default: return 'bg-yellow-500';
  }
}

interface TaskCardProps extends Omit<React.ComponentProps<typeof KanbanItem>, 'value' | 'children'> {
  task: Task;
  asHandle?: boolean;
}

export function TaskCard({ task, asHandle, ...props }: TaskCardProps) {
  const cardContent = (
    <div className="rounded-md border bg-card p-3 shadow-xs">
      <div className="flex flex-col gap-2.5">
        <div className="flex items-center justify-between gap-2">
          <span className="line-clamp-1 font-medium text-sm">{task.title}</span>
          <Badge
            variant="outline"
            className="pointer-events-none h-5 rounded-sm px-1.5 text-[11px] capitalize shrink-0"
          >
            <BadgeDot className={cn(getPriorityDotColor(task.priority))} /> {task.priority}
          </Badge>
        </div>
        {task.description && (
          <p className="text-muted-foreground text-xs line-clamp-2">{task.description}</p>
        )}
        <div className="flex items-center justify-between text-muted-foreground text-xs">
          {task.dueDate && (
            <div className="flex items-center gap-1 text-[10px] tabular-nums whitespace-nowrap">
              <Calendar className="size-3" /><time>{task.dueDate}</time>
            </div>
          )}
        </div>
      </div>
    </div>
  );

  return (
    <KanbanItem value={task.id} {...props}>
      {asHandle ? <KanbanItemHandle>{cardContent}</KanbanItemHandle> : cardContent}
    </KanbanItem>
  );
}
