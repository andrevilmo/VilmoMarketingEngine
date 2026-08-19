import { useEffect, useMemo, useState } from 'react';
import { isEqual, startOfDay } from 'date-fns';
import { Calendar as CalendarIcon } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { getDatePresets } from '@/app/real-estate/mock';

export function Dates() {
  const today = useMemo(() => new Date(), []);
  const presets = useMemo(() => getDatePresets(today), [today]);

  const [month, setMonth] = useState(today);
  const defaultPreset = presets[0];
  const [date, setDate] = useState(defaultPreset.range);
  const [selectedPreset, setSelectedPreset] = useState(defaultPreset.label);
  const [isPopoverOpen, setIsPopoverOpen] = useState(false);

  const handleApply = () => {
    if (date) {
      setDate(date);
    }
    setIsPopoverOpen(false);
  };

  const handleReset = () => {
    setDate(defaultPreset.range);
    setSelectedPreset(defaultPreset.label);
    setIsPopoverOpen(false);
  };

  const handleSelect = (selected) => {
    setDate({
      from: selected?.from || undefined,
      to: selected?.to || undefined,
    });
    setSelectedPreset(null);
  };

  useEffect(() => {
    const matchedPreset = presets.find(
      (preset) =>
        isEqual(
          startOfDay(preset.range.from),
          startOfDay(date?.from || new Date(0)),
        ) &&
        isEqual(
          startOfDay(preset.range.to),
          startOfDay(date?.to || new Date(0)),
        ),
    );
    setSelectedPreset(matchedPreset?.label || null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [date]);

  return (
    <Popover open={isPopoverOpen} onOpenChange={setIsPopoverOpen}>
      <PopoverTrigger asChild>
        <Button variant="outline">
          <CalendarIcon className="size-3" />
          When
        </Button>
      </PopoverTrigger>

      <PopoverContent className="w-auto p-0" align="center">
        <div className="flex max-sm:flex-col">
          <div className="relative border-border max-sm:order-1 max-sm:border-t sm:w-32">
            <div className="h-full border-border sm:border-e py-2">
              <div className="flex flex-col px-2 gap-[2px]">
                {presets.map((preset, index) => (
                  <Button
                    key={index}
                    type="button"
                    variant="ghost"
                    className={cn(
                      'h-8 w-full justify-start',
                      selectedPreset === preset.label && 'bg-accent',
                    )}
                    onClick={() => {
                      setDate(preset.range);
                      // Update the calendar to show the starting month of the selected range
                      setMonth(preset.range.from || today);
                      setSelectedPreset(preset.label); // Explicitly set the active preset
                    }}
                  >
                    {preset.label}
                  </Button>
                ))}
              </div>
            </div>
          </div>
          <Calendar
            autoFocus
            mode="range"
            month={month}
            onMonthChange={setMonth}
            showOutsideDays={false}
            selected={date}
            onSelect={handleSelect}
            numberOfMonths={2}
          />
        </div>
        <div className="flex items-center justify-end gap-1.5 border-t border-border p-3">
          <Button variant="outline" onClick={handleReset}>
            Reset
          </Button>
          <Button variant="mono" onClick={handleApply}>
            Apply
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}
