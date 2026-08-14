import * as React from 'react';
import { Newspaper } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandCheck,
  CommandEmpty,
  CommandGroup,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { conditions } from '@/app/real-estate/mock';

export function Condition() {
  const [open, setOpen] = React.useState(false);
  const [value, setValue] = React.useState('New');

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="outline">
          <Newspaper className="size-3" />
          Condition
        </Button>
      </PopoverTrigger>

      <PopoverContent className="w-auto p-0" align="center">
        <Command>
          <CommandList>
            <CommandEmpty>No conditions found.</CommandEmpty>
            <CommandGroup>
              {conditions.map((condition) => (
                <CommandItem
                  key={condition.value}
                  value={condition.value}
                  onSelect={(currentValue) => {
                    setValue(currentValue === value ? '' : currentValue);
                    setOpen(false);
                  }}
                >
                  <span className="truncate">{condition.label}</span>
                  <CommandCheck
                    className={cn(
                      'ms-auto',
                      value === condition.value ? 'opacity-100' : 'opacity-0',
                    )}
                  />
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
