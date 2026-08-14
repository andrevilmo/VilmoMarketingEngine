import { Calendar, Clock } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Badge, BadgeDot } from '@/components/ui/badge';
import {
  Card,
  CardContent,
  CardHeader,
  CardHeading,
  CardTitle,
} from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';

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

export function TaskItem({ task, onToggle }) {
  return (
    <div
      className={cn(
        'flex items-center gap-4 px-5 py-4 transition-colors hover:bg-muted/50',
        task.completed && 'bg-muted/40',
      )}
    >
      <Checkbox
        checked={task.completed}
        onCheckedChange={() => onToggle(task.id)}
        className="shrink-0"
      />

      <div className="flex-1 min-w-0">
        <p
          className={cn(
            'text-sm font-medium',
            task.completed && 'text-sm line-through text-muted-foreground',
          )}
        >
          {task.title}
        </p>
        <div className="flex items-center gap-3 text-xs text-muted-foreground mt-1">
          <span className="text-xs text-muted-foreground flex items-center gap-1">
            <Clock className="size-3" /> {task.time}
          </span>
        </div>
      </div>
      <Badge variant="outline">
        <BadgeDot className={getPriorityDotColor(task.priority)} />{' '}
        {task.priority}
      </Badge>
    </div>
  );
}

export function TaskList({ tasks, onToggleTask }) {
  // Group tasks by date
  const groupedTasks = tasks.reduce((groups, task) => {
    const date = task.date;
    if (!groups[date]) {
      groups[date] = [];
    }
    groups[date].push(task);
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
              </CardTitle>
            </CardHeading>
          </CardHeader>
          <CardContent className="p-0">
            <div className="divide-y divide-border">
              {dateTasks.map((task) => (
                <TaskItem key={task.id} task={task} onToggle={onToggleTask} />
              ))}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
