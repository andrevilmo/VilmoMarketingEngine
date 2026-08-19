import { Routes, Route, Navigate } from 'react-router-dom';
import { DefaultLayout } from './layout';
import { AllTasksPage } from './pages/all-tasks/page';
import { TodayPage } from './pages/today/page';
import { UpcomingPage } from './pages/upcoming/page';
import { PriorityPage } from './pages/priority/page';
import { CompletedPage } from './pages/completed/page';

export default function TodoModule() {
  return (
    <Routes>
      <Route element={<DefaultLayout />}>
        <Route index element={<Navigate to="all-tasks" replace />} />
        <Route path="all-tasks" element={<AllTasksPage />} />
        <Route path="today" element={<TodayPage />} />
        <Route path="upcoming" element={<UpcomingPage />} />
        <Route path="priority" element={<PriorityPage />} />
        <Route path="completed" element={<CompletedPage />} />
      </Route>
    </Routes>
  );
}
