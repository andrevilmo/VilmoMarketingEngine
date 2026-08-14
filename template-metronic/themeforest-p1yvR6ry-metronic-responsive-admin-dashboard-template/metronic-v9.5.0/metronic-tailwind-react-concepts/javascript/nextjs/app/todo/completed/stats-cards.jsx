import { Check, TrendingUp, Trophy } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { ProgressCircle } from '@/components/ui/progress';

export function TotalCompletedCard({ count }) {
  return (
    <Card className="min-w-0">
      <CardContent className="flex items-center gap-3 sm:gap-4 lg:gap-5 p-4 sm:p-5 lg:p-6">
        <div className="shrink-0 size-11 sm:size-12 lg:size-14.5 rounded-full border-4 sm:border-5 border-green-400 flex items-center justify-center">
          <Check className="size-5 sm:size-5.5 lg:size-6 text-green-400" />
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-base font-medium leading-none text-foreground">
            {count} tasks
          </h1>
          <p className="text-sm font-normal text-muted-foreground">
            Total completed
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

export function ThisWeekCard({ count }) {
  return (
    <Card className="min-w-0">
      <CardContent className="flex items-center gap-3 sm:gap-4 lg:gap-5 p-4 sm:p-5 lg:p-6">
        <div className="shrink-0 size-11 sm:size-12 lg:size-14.5 rounded-full border-4 sm:border-5 border-blue-400 flex items-center justify-center">
          <Trophy className="size-5 sm:size-5.5 lg:size-6 text-blue-400" />
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-base font-medium leading-none text-foreground">
            {count} tasks
          </h1>
          <p className="text-sm font-normal text-muted-foreground">
            Completed this week
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

export function ProductivityCard({ percentage }) {
  return (
    <Card className="min-w-0">
      <CardContent className="flex items-center gap-3 sm:gap-4 lg:gap-5 p-4 sm:p-5 lg:p-6">
        <div className="shrink-0">
          <ProgressCircle
            value={percentage}
            size={48}
            strokeWidth={5}
            indicatorClassName={
              percentage >= 80
                ? 'text-green-500'
                : percentage >= 50
                  ? 'text-primary'
                  : 'text-yellow-500'
            }
            className="sm:hidden"
          >
            <TrendingUp className="size-4 text-muted-foreground" />
          </ProgressCircle>
          <ProgressCircle
            value={percentage}
            size={56}
            strokeWidth={6}
            indicatorClassName={
              percentage >= 80
                ? 'text-green-500'
                : percentage >= 50
                  ? 'text-primary'
                  : 'text-yellow-500'
            }
            className="hidden sm:flex lg:hidden"
          >
            <TrendingUp className="size-4 text-muted-foreground" />
          </ProgressCircle>
          <ProgressCircle
            value={percentage}
            size={60}
            strokeWidth={6}
            indicatorClassName={
              percentage >= 80
                ? 'text-green-500'
                : percentage >= 50
                  ? 'text-primary'
                  : 'text-yellow-500'
            }
            className="hidden lg:flex"
          >
            <TrendingUp className="size-5 text-muted-foreground" />
          </ProgressCircle>
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-base font-medium leading-none text-foreground">
            {percentage}%
          </h1>
          <p className="text-sm font-normal text-muted-foreground">
            Productivity score
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
