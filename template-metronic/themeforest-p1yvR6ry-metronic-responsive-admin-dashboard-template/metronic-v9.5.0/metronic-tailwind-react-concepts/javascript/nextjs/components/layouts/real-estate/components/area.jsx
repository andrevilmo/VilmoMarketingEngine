import * as React from 'react';
import { MapPin } from 'lucide-react';
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
import { topAreas } from '@/app/real-estate/mock';

export function Area() {
  const [open, setOpen] = React.useState(false);
  const [value, setValue] = React.useState('Manhattan, NY');

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="outline">
          <MapPin className="size-3" />
          Where
        </Button>
      </PopoverTrigger>

      <PopoverContent className="w-auto p-0" align="center">
        <Command>
          <CommandList>
            <CommandEmpty>No boroughs found.</CommandEmpty>
            <CommandGroup>
              {topAreas.map((area) => (
                <CommandItem
                  key={area.value}
                  value={area.value}
                  onSelect={(currentValue) => {
                    setValue(currentValue === value ? '' : currentValue);
                    setOpen(false);
                  }}
                >
                  <span className="truncate">{area.label}</span>
                  <CommandCheck
                    className={cn(
                      'ms-auto',
                      value === area.value ? 'opacity-100' : 'opacity-0',
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
