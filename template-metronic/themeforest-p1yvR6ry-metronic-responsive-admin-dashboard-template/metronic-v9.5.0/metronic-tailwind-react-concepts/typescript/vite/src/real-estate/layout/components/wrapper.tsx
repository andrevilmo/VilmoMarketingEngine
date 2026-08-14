import { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Header } from './header';
import { cn } from '@/lib/utils';

export function Wrapper() {
  const [enableTransitions, setEnableTransitions] = useState(false);

  useEffect(() => {
    const id = requestAnimationFrame(() => setEnableTransitions(true));
    return () => cancelAnimationFrame(id);
  }, []);

  return (
    <>
      <Header />

      <main
        className={
          cn(
            'flex flex-col grow pt-(--header-height-mobile) lg:pt-(--header-height) bg-muted/50',
            enableTransitions ? 'lg:transition-[margin] lg:duration-300' : 'lg:transition-none',
          )
        }
        role="content"
      >
        <Outlet />
      </main>
    </>
  );
}
