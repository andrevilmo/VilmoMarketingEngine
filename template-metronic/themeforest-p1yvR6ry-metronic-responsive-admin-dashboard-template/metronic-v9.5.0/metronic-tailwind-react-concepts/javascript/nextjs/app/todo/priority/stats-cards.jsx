import { AlertCircle, ArrowDown, Flame } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';

export function HighPriorityCard({ count }) {
  return (
    <Card className="min-w-0">
      <CardContent className="flex items-center gap-3 sm:gap-4 lg:gap-5 p-4 sm:p-5 lg:p-6">
        <div className="shrink-0 size-11 sm:size-12 lg:size-14.5 rounded-full border-4 sm:border-5 border-red-400 flex items-center justify-center">
          <Flame className="size-5 sm:size-5.5 lg:size-6 text-red-400" />
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-base font-medium leading-none text-foreground">
            {count} tasks
          </h1>
          <p className="text-sm font-normal text-muted-foreground">
            High priority
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

export function MediumPriorityCard({ count }) {
  return (
    <Card className="min-w-0">
      <CardContent className="flex items-center gap-3 sm:gap-4 lg:gap-5 p-4 sm:p-5 lg:p-6">
        <div className="shrink-0 size-11 sm:size-12 lg:size-14.5 rounded-full border-4 sm:border-5 border-amber-400 flex items-center justify-center">
          <AlertCircle className="size-5 sm:size-5.5 lg:size-6 text-amber-400" />
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-base font-medium leading-none text-foreground">
            {count} tasks
          </h1>
          <p className="text-sm font-normal text-muted-foreground">
            Medium priority
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

export function LowPriorityCard({ count }) {
  return (
    <Card className="min-w-0">
      <CardContent className="flex items-center gap-3 sm:gap-4 lg:gap-5 p-4 sm:p-5 lg:p-6">
        <div className="shrink-0 size-11 sm:size-12 lg:size-14.5 rounded-full border-4 sm:border-5 border-blue-400 flex items-center justify-center">
          <ArrowDown className="size-5 sm:size-5.5 lg:size-6 text-blue-400" />
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-base font-medium leading-none text-foreground">
            {count} tasks
          </h1>
          <p className="text-sm font-normal text-muted-foreground">
            Low priority
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
