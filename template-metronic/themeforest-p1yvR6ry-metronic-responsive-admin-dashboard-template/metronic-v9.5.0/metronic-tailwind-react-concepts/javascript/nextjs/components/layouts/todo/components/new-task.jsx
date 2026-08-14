import { useState } from 'react';
import { Plus } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { priorityOptions } from '@/app/todo/mock';

export function NewTask({ isCollapsed }) {
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [newListName, setNewListName] = useState('');
  const [priority, setPriority] = useState('medium');

  return (
    <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
      {isCollapsed ? (
        <Tooltip>
          <TooltipTrigger asChild>
            <DialogTrigger asChild>
              <Button className="mx-auto w-full" variant="mono" size="icon">
                <Plus className="size-4" />
              </Button>
            </DialogTrigger>
          </TooltipTrigger>
          <TooltipContent side="right">New Task</TooltipContent>
        </Tooltip>
      ) : (
        <DialogTrigger asChild>
          <Button className="mx-auto w-full" variant="mono">
            <Plus className="size-4" />
            <span className="font-semibold">New Task</span>
          </Button>
        </DialogTrigger>
      )}

      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>New Task</DialogTitle>
        </DialogHeader>

        <Input
          value={newListName}
          onChange={(event) => setNewListName(event.target.value)}
          placeholder="Add a new task"
          autoFocus
        />

        <DialogFooter className="flex-row justify-between sm:justify-between gap-3">
          <Select
            value={priority}
            onValueChange={setPriority}
            indicatorPosition="right"
          >
            <SelectTrigger className="w-[130px]">
              <SelectValue placeholder="Priority" />
            </SelectTrigger>
            <SelectContent>
              {priorityOptions.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  <span className="flex items-center gap-2">
                    <span
                      className={cn('size-1.5 rounded-full', option.color)}
                    />
                    {option.label}
                  </span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            variant="mono"
            disabled={!newListName.trim()}
            onClick={() => {
              setIsCreateDialogOpen(false);
              setNewListName('');
              setPriority('medium');
            }}
          >
            Continue
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
