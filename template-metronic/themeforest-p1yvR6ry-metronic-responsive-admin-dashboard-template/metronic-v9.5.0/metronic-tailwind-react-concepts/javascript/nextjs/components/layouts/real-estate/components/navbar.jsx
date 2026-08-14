import { useState } from 'react';
import { SlidersHorizontal } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { ScrollArea, ScrollBar } from '@/components/ui/scroll-area';
import { filtersConfig } from '@/app/real-estate/mock';
import { useLayout } from './context';
import { NavbarSearch } from './navbar-search';

export function Navbar() {
  const { isMobile } = useLayout();
  const [activeFilters, setActiveFilters] = useState({
    propertyType: true,
    condition: true,
    area: true,
    price: true,
    dates: true,
  });

  const toggleFilter = (key) => {
    setActiveFilters((prev) => ({
      ...prev,
      [key]: !prev[key],
    }));
  };

  const activeCount = Object.values(activeFilters).filter(Boolean).length;

  return (
    <div className="container-fluid flex items-center justify-between w-full h-(--navbar-height) lg:in-data-[header-sticky=true]:hidden border-b border-border">
      <ScrollArea>
        <div className="flex gap-2">
          <Popover>
            <PopoverTrigger asChild>
              <Button variant="outline">
                <SlidersHorizontal className="size-3" />
                Filters
                {activeCount < filtersConfig.length && (
                  <span className="ml-1 text-xs text-muted-foreground">
                    ({activeCount}/{filtersConfig.length})
                  </span>
                )}
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-56" align="start">
              <div className="space-y-3">
                <div className="space-y-2">
                  {filtersConfig.map((filter) => (
                    <div key={filter.key} className="flex items-center gap-2">
                      <Checkbox
                        id={filter.key}
                        checked={activeFilters[filter.key]}
                        onCheckedChange={() => toggleFilter(filter.key)}
                      />

                      <Label htmlFor={filter.key} className="cursor-pointer">
                        {filter.label}
                      </Label>
                    </div>
                  ))}
                </div>
              </div>
            </PopoverContent>
          </Popover>

          {filtersConfig.map((filter) => {
            if (!activeFilters[filter.key]) return null;
            const Component = filter.component;
            return <Component key={filter.key} />;
          })}
          <Button variant="mono">Save Search</Button>
        </div>

        <ScrollBar orientation="horizontal" />
      </ScrollArea>

      {!isMobile && <NavbarSearch />}
    </div>
  );
}
