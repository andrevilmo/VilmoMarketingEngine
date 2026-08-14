import { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { HeaderMobile } from './header-mobile';
import { useLayout } from './context';
import { Sidebar } from './sidebar';
import { Aside } from './aside';
import { cn } from '@/lib/utils';

export function Wrapper() {
  const {isMobile} = useLayout();
  const [enableTransitions, setEnableTransitions] = useState(false);

  useEffect(() => {
    const id = requestAnimationFrame(() => setEnableTransitions(true));
    return () => cancelAnimationFrame(id);
  }, []);

  return (
    <>
      {isMobile && <HeaderMobile />}

      <div className="flex flex-col lg:flex-row grow pt-(--header-height-mobile) lg:pt-0 mb-2.5 lg:my-2.5">
        <div className="flex grow rounded-xl mt-0">
          {!isMobile && <Sidebar />}
          {!isMobile && <Aside />}

          <div
            className={cn(
              'grow lg:overflow-y-auto lg:ms-[calc(var(--sidebar-width-collapsed)+20px)] lg:in-data-[sidebar-open=true]:ms-[calc(var(--sidebar-width)+20px)] mx-2 lg:me-2 lg:in-data-[aside-open=true]:me-[calc(var(--aside-width)+20px)] bg-background border border-input rounded-xl shadow-xs',
              enableTransitions ? 'lg:transition-[margin] lg:duration-300' : 'lg:transition-none',
            )}
          >
            <main className="grow">
              <Outlet />
            </main>
          </div>
        </div>
      </div>
    </>
  );
}
