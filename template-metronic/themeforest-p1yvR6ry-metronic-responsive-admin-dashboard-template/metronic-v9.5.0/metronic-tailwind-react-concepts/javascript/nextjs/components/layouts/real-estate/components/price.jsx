import { useId, useState } from 'react';
import { DollarSign } from 'lucide-react';
import { useSliderInput } from '@/hooks/use-slider-input';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Slider, SliderThumb } from '@/components/ui/slider';
import { priceItems } from '@/app/real-estate/mock';

export function Price() {
  const [open, setOpen] = useState(false);
  const id = useId();
  const minValue = Math.min(...priceItems.map((item) => item.price));
  const maxValue = Math.max(...priceItems.map((item) => item.price));
  const {
    sliderValues,
    inputValues,
    handleSliderChange,
    handleInputChange,
    validateAndUpdateValue,
  } = useSliderInput({
    minValue,
    maxValue,
    initialValue: [200, 800],
  });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="outline">
          <DollarSign className="size-3" />
          Price
        </Button>
      </DialogTrigger>

      <DialogContent className="sm:max-w-md">
        <DialogBody>
          <div className="space-y-4">
            {/* Slider */}
            <div className="flex flex-col gap-2.5">
              <Label>Price Range</Label>
              <Slider
                value={sliderValues}
                onValueChange={handleSliderChange}
                min={minValue}
                max={maxValue}
                step={10}
                className="[&>span:first-child]:bg-zinc-100 dark:[&>span:first-child]:bg-zinc-900 [&>span:first-child>span]:bg-zinc-900 dark:[&>span:first-child>span]:bg-zinc-100"
              >
                <SliderThumb className="size-2 border-zinc-900 dark:border-zinc-100 dark:bg-zinc-900" />
                <SliderThumb className="size-2 border-zinc-900 dark:border-zinc-100 dark:bg-zinc-900" />
              </Slider>
            </div>
            {/* Inputs */}
            <div className="flex items-center justify-between gap-4">
              <div className="space-y-2.5">
                <Label htmlFor={`${id}-min`}>Min Price</Label>
                <Input
                  id={`${id}-min`}
                  type="number"
                  value={inputValues[0]}
                  onChange={(e) => handleInputChange(e, 0)}
                  onBlur={() => validateAndUpdateValue(inputValues[0], 0)}
                  placeholder={`$${minValue}`}
                />
              </div>
              <div className="space-y-2.5">
                <Label htmlFor={`${id}-max`}>Max Price</Label>
                <Input
                  id={`${id}-max`}
                  type="number"
                  value={inputValues[1]}
                  onChange={(e) => handleInputChange(e, 1)}
                  onBlur={() => validateAndUpdateValue(inputValues[1], 1)}
                  placeholder={`$${maxValue}`}
                />
              </div>
            </div>
          </div>
        </DialogBody>

        <DialogFooter>
          <DialogClose asChild>
            <Button type="button" variant="outline">
              Close
            </Button>
          </DialogClose>
          <Button variant="mono" type="submit">
            Apply
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
