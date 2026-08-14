'use client';

import { LayoutProvider } from './components/context';
import { Wrapper } from './components/wrapper';

export function DefaultLayout({ children }) {
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
        }}
      >
        <Wrapper>{children}</Wrapper>
      </LayoutProvider>
    </>
  );
}
