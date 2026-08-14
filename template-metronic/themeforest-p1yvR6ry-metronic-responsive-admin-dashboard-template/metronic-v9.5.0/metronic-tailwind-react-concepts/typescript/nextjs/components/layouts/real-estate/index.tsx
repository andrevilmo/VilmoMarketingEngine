'use client';

import { Wrapper } from './components/wrapper';
import { LayoutProvider } from './components/context';

export function DefaultLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <LayoutProvider
        bodyClassName="lg:overflow-hidden"
        style={{
          '--header-height': '120px',
          '--navbar-height': '60px',
          '--header-height-sticky': '70px',
          '--header-height-mobile': '120px',
        } as React.CSSProperties}
      >
        <Wrapper>
          {children}
        </Wrapper>
      </LayoutProvider>
    </>
  );
}
