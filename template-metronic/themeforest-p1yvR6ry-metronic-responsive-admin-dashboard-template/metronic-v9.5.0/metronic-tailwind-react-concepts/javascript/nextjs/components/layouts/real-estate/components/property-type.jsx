import * as React from 'react';
import { House } from 'lucide-react';
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
import { propertyTypes } from '@/app/real-estate/mock';

export function PropertyType() {
  const [open, setOpen] = React.useState(false);
  const [value, setValue] = React.useState('House');

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="outline">
          <House className="size-3" />
          Type
        </Button>
      </PopoverTrigger>

      <PopoverContent className="w-auto p-0" align="center">
        <Command>
          <CommandList>
            <CommandEmpty>No property types found.</CommandEmpty>
            <CommandGroup>
              {propertyTypes.map((propertyType) => (
                <CommandItem
                  key={propertyType.value}
                  value={propertyType.value}
                  onSelect={(currentValue) => {
                    setValue(currentValue === value ? '' : currentValue);
                    setOpen(false);
                  }}
                >
                  <span className="truncate">{propertyType.label}</span>
                  <CommandCheck
                    className={cn(
                      'ms-auto',
                      value === propertyType.value
                        ? 'opacity-100'
                        : 'opacity-0',
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
