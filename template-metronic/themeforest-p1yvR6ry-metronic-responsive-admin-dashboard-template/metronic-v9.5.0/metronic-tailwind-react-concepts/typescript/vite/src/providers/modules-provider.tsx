import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { ScreenLoader } from '@/components/screen-loader';

const LazyCrmModule = lazy(() => import('@/crm'));
const LazyStoreInventoryModule = lazy(() => import('@/store-inventory'));
const LazyMailModule = lazy(() => import('@/mail'));
const LazyCalendarModule = lazy(() => import('@/calendar'));
const LazyAIModule = lazy(() => import('@/ai'));
const LazyTodoModule = lazy(() => import('@/todo'));
const LazyRealEstateModule = lazy(() => import('@/real-estate'));

export function ModulesProvider() {
  const location = useLocation();
  const path = location.pathname;

  // Detect if current path is for CRM or Store
  const isCrm = path.startsWith('/crm');
  const isMail = path.startsWith('/mail');
  const isStoreInventory = path.startsWith('/store-inventory');
  const isCalendar = path.startsWith('/calendar');
  const isAI = path.startsWith('/ai');
  const isTodo = path.startsWith('/todo');
  const isRealEstate = path.startsWith('/real-estate');

  if (isCrm) {
    return (
      <Routes>
        <Route
          path="/crm/*"
          element={
            <Suspense fallback={<ScreenLoader />}>
              <LazyCrmModule />
            </Suspense>
          }
        />
      </Routes>
    );
  } else if (isStoreInventory) {
    return (
      <Routes>
        <Route
          path="/store-inventory/*"
          element={
            <Suspense fallback={<ScreenLoader />}>
              <LazyStoreInventoryModule />
            </Suspense>
          }
        />
      </Routes>
    );
  } else if (isMail) {
    return (
      <Routes>
        <Route path="/mail/*" element={ 
          <Suspense fallback={<ScreenLoader />}> 
            <LazyMailModule /> 
          </Suspense> 
        } />
      </Routes>
    );
  } else if (isCalendar) {
    return (
      <Routes>
        <Route
          path="/calendar/*"
          element={
            <Suspense fallback={<ScreenLoader />}>
              <LazyCalendarModule />
            </Suspense>
          }
        />
      </Routes>
    );
  } else if (isAI) {
    return (
      <Routes>
        <Route
          path="/ai/*"
          element={
            <Suspense fallback={<ScreenLoader />}>
              <LazyAIModule />
            </Suspense>
          }
        />
      </Routes>
    );
  } else if (isTodo) {
    return (
      <Routes>
        <Route
          path="/todo/*"
          element={
            <Suspense fallback={<ScreenLoader />}>
              <LazyTodoModule />
            </Suspense>
          }
        />
      </Routes>
    );
  } else if (isRealEstate) {
    return (
      <Routes>
        <Route
          path="/real-estate/*"
          element={
            <Suspense fallback={<ScreenLoader />}>
              <LazyRealEstateModule />
            </Suspense>
          }
        />
      </Routes>
    );
  }

  // Redirect to CRM if no route matches
  return <Navigate to="/crm" replace />;
}
