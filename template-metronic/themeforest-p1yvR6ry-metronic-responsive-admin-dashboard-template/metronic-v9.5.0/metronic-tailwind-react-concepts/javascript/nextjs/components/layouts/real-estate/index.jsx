'use client';

import { LayoutProvider } from './components/context';
import { Wrapper } from './components/wrapper';

export function DefaultLayout({ children }) {
  return (
    <>
      <LayoutProvider
        bodyClassName="lg:overflow-hidden"
        style={{
          '--header-height': '120px',
          '--navbar-height': '60px',
          '--header-height-sticky': '70px',
          '--header-height-mobile': '120px',
        }}
      >
        <Wrapper>{children}</Wrapper>
      </LayoutProvider>
    </>
  );
}
