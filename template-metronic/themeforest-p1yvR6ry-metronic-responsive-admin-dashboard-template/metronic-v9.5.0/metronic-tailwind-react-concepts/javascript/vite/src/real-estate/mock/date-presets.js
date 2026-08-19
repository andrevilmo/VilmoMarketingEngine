import { startOfMonth, startOfYear, subDays } from 'date-fns';

export function getDatePresets(today) {
  return [
    { label: 'Today', range: { from: today, to: today } },
    { label: 'Last 7 days', range: { from: subDays(today, 6), to: today } },
    { label: 'Last 30 days', range: { from: subDays(today, 29), to: today } },
    { label: 'Month to date', range: { from: startOfMonth(today), to: today } },
    { label: 'Year to date', range: { from: startOfYear(today), to: today } },
  ];
}
