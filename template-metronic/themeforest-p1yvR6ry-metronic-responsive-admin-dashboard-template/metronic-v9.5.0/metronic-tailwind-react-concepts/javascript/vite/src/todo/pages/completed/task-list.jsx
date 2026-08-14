import { Calendar, CheckCircle2, Clock, Trash2 } from 'lucide-react';
import { Badge, BadgeDot } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardHeader,
  CardHeading,
  CardTitle,
} from '@/components/ui/card';

function getPriorityDotColor(priority) {
  switch (priority) {
    case 'high':
      return 'bg-red-500';
    case 'medium':
      return 'bg-blue-500';
    default:
      return 'bg-yellow-500';
  }
}

export function TaskItem({ task, onDelete }) {
  return (
    <div className="flex items-center gap-4 px-5 py-4 transition-colors hover:bg-muted/50 group">
      <div className="shrink-0 size-5 rounded-full flex items-center justify-center">
        <CheckCircle2 className="size-5 sm:size-5.5 lg:size-6 text-green-400" />
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium text-muted-foreground line-through">
          {task.title}
        </p>
        <div className="flex items-center gap-3 text-xs text-muted-foreground mt-1">
          <span className="flex items-center gap-1">
            <Clock className="size-3" /> {task.completedAt}
          </span>
          {task.category && (
            <span className="text-muted-foreground/70">{task.category}</span>
          )}
        </div>
      </div>
      <Button
        mode="icon"
        variant="ghost"
        size="sm"
        className="opacity-0 group-hover:opacity-100 transition-opacity"
        onClick={() => onDelete(task.id)}
      >
        <Trash2 className="size-4 text-muted-foreground hover:text-destructive" />
      </Button>
      <Badge variant="outline" className="opacity-50">
        <BadgeDot className={getPriorityDotColor(task.priority)} />{' '}
        {task.priority}
      </Badge>
    </div>
  );
}

export function TaskList({ tasks, onDeleteTask }) {
  // Group tasks by date (extract date from completedAt)
  const groupedTasks = tasks.reduce((groups, task) => {
    // Extract date part (e.g., "Today", "Yesterday", "Dec 16")
    const datePart = task.completedAt.split(',')[0];
    if (!groups[datePart]) {
      groups[datePart] = [];
    }
    groups[datePart].push(task);
    return groups;
  }, {});

  return (
    <div className="space-y-6">
      {Object.entries(groupedTasks).map(([date, dateTasks]) => (
        <Card key={date}>
          <CardHeader>
            <CardHeading>
              <CardTitle className="flex items-center gap-2">
                <Calendar className="size-4 text-muted-foreground" />
                <span className="text-sm font-medium leading-none text-foreground">
                  {date}
                </span>
                <span className="text-xs text-muted-foreground">
                  ({dateTasks.length}{' '}
                  {dateTasks.length === 1 ? 'task' : 'tasks'})
                </span>
              </CardTitle>
            </CardHeading>
          </CardHeader>
          <CardContent className="p-0">
            <div className="divide-y divide-border">
              {dateTasks.map((task) => (
                <TaskItem key={task.id} task={task} onDelete={onDeleteTask} />
              ))}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
