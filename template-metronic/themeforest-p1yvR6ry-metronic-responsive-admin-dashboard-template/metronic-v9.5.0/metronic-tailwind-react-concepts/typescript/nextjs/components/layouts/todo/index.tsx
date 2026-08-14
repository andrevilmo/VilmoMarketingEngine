'use client';

import { Wrapper } from './components/wrapper';
import { LayoutProvider } from './components/context';

export function DefaultLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <LayoutProvider
        bodyClassName="bg-zinc-100 dark:bg-zinc-900 lg:overflow-hidden"
        style={{
          '--sidebar-width': '250px',
          '--sidebar-width-mobile': '225px',
          '--sidebar-width-collapsed': '60px',
          '--aside-width': '320px',
          '--aside-width-mobile': '320px',
          '--header-height': '60px',
          '--header-height-mobile': '70px',
        } as React.CSSProperties}
      >
        <Wrapper>
          {children}
        </Wrapper>
      </LayoutProvider>
    </>
  );
}
