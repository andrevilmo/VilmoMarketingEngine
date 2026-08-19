import { AlertCircle, ArrowDown, Calendar, Flame } from 'lucide-react';
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

function getPriorityIcon(priority) {
  switch (priority) {
    case 'high':
      return <Flame className="size-4 text-red-500" />;
    case 'medium':
      return <AlertCircle className="size-4 text-yellow-500" />;
    default:
      return <ArrowDown className="size-4 text-blue-500" />;
  }
}

function getPriorityLabel(priority) {
  switch (priority) {
    case 'high':
      return 'High Priority';
    case 'medium':
      return 'Medium Priority';
    default:
      return 'Low Priority';
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
            task.completed && 'line-through text-muted-foreground',
          )}
        >
          {task.title}
        </p>
        <div className="flex items-center gap-3 text-xs text-muted-foreground mt-1">
          {task.dueDate && (
            <span className="flex items-center gap-1">
              <Calendar className="size-3" /> {task.dueDate}
            </span>
          )}
          {task.category && (
            <span className="text-muted-foreground/70">{task.category}</span>
          )}
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
  // Group tasks by priority
  const priorityOrder = ['high', 'medium', 'low'];

  const groupedTasks = priorityOrder.reduce((groups, priority) => {
    const priorityTasks = tasks.filter((t) => t.priority === priority);
    if (priorityTasks.length > 0) {
      groups[priority] = priorityTasks;
    }
    return groups;
  }, {});

  return (
    <div className="space-y-6">
      {priorityOrder.map((priority) => {
        const priorityTasks = groupedTasks[priority];
        if (!priorityTasks || priorityTasks.length === 0) return null;

        const pendingCount = priorityTasks.filter((t) => !t.completed).length;

        return (
          <Card key={priority}>
            <CardHeader>
              <CardHeading>
                <CardTitle className="flex items-center gap-2">
                  {getPriorityIcon(priority)}
                  <span className="text-sm font-medium leading-none text-foreground">
                    {getPriorityLabel(priority)}
                  </span>
                  <span className="text-xs text-muted-foreground">
                    ({pendingCount} pending)
                  </span>
                </CardTitle>
              </CardHeading>
            </CardHeader>
            <CardContent className="p-0">
              <div className="divide-y divide-border">
                {priorityTasks.map((task) => (
                  <TaskItem key={task.id} task={task} onToggle={onToggleTask} />
                ))}
              </div>
            </CardContent>
          </Card>
        );
      })}
    </div>
  );
}
