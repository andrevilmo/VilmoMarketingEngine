import { Routes, Route, Navigate } from 'react-router-dom';
import { DefaultLayout } from './layout';
import { Page } from './pages/page';

export default function RealEstateModule() {
  return (
    <Routes>
      <Route element={<DefaultLayout />}>
        <Route index element={<Navigate to="page" replace />} />
        <Route path="page" element={<Page />} />
      </Route>
    </Routes>
  );
}
