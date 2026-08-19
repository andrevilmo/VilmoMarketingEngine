import { Flame, TrendingUp } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { ProgressCircle } from '@/components/ui/progress';

export function ProgressCard({ completedCount, totalCount, progressPercent }) {
  return (
    <Card className="min-w-0">
      <CardContent className="flex items-center gap-3 sm:gap-4 lg:gap-5 p-4 sm:p-5 lg:p-6">
        <div className="shrink-0">
          <ProgressCircle
            value={progressPercent}
            size={48}
            strokeWidth={5}
            indicatorClassName={
              progressPercent === 100 ? 'text-green-500' : 'text-primary'
            }
            className="sm:hidden"
          >
            <span className="text-base font-medium leading-none text-foreground">
              {progressPercent}%
            </span>
          </ProgressCircle>
          <ProgressCircle
            value={progressPercent}
            size={56}
            strokeWidth={6}
            indicatorClassName={
              progressPercent === 100 ? 'text-green-500' : 'text-primary'
            }
            className="hidden sm:flex lg:hidden"
          >
            <span className="text-base font-medium leading-none text-foreground">
              {progressPercent}%
            </span>
          </ProgressCircle>
          <ProgressCircle
            value={progressPercent}
            size={60}
            strokeWidth={6}
            indicatorClassName={
              progressPercent === 100 ? 'text-green-500' : 'text-primary'
            }
            className="hidden lg:flex"
          >
            <span className="text-base font-medium leading-none text-foreground">
              {progressPercent}%
            </span>
          </ProgressCircle>
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-base font-medium leading-none text-foreground">
            {completedCount}/{totalCount}
          </h1>
          <p className="text-sm font-normal text-muted-foreground">
            Tasks completed
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

export function HighPriorityCard({ count }) {
  return (
    <Card className="min-w-0">
      <CardContent className="flex items-center gap-3 sm:gap-4 lg:gap-5 p-4 sm:p-5 lg:p-6">
        <div className="shrink-0 size-11 sm:size-12 lg:size-14.5 rounded-full border-4 sm:border-5 border-red-400 flex items-center justify-center">
          <Flame className="size-5 sm:size-5.5 lg:size-6 text-red-400" />
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-base font-medium leading-none text-foreground">
            {count} task
          </h1>
          <p className="text-sm font-normal text-muted-foreground">
            High priority
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

export function StreakCard({ days }) {
  return (
    <Card className="min-w-0">
      <CardContent className="flex items-center gap-3 sm:gap-4 lg:gap-5 p-4 sm:p-5 lg:p-6">
        <div className="shrink-0 size-11 sm:size-12 lg:size-14.5 rounded-full border-4 sm:border-5 border-green-400 flex items-center justify-center">
          <TrendingUp className="size-5 sm:size-5.5 lg:size-6 text-green-400" />
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-base font-medium leading-none text-foreground">
            {days} days
          </h1>
          <p className="text-sm font-normal text-muted-foreground">
            Current streak
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
