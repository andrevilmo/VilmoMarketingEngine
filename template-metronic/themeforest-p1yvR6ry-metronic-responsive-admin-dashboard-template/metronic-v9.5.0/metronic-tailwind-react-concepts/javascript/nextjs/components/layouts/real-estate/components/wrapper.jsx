import { useEffect, useState } from 'react';
import { cn } from '@/lib/utils';
import { Header } from './header';

export function Wrapper({ children }) {
  const [enableTransitions, setEnableTransitions] = useState(false);

  useEffect(() => {
    const id = requestAnimationFrame(() => setEnableTransitions(true));
    return () => cancelAnimationFrame(id);
  }, []);

  return (
    <>
      <Header />

      <main
        className={cn(
          'flex flex-col grow pt-(--header-height-mobile) lg:pt-(--header-height) bg-muted/50',
          enableTransitions
            ? 'lg:transition-[margin] lg:duration-300'
            : 'lg:transition-none',
        )}
        role="content"
      >
        {children}
      </main>
    </>
  );
}
