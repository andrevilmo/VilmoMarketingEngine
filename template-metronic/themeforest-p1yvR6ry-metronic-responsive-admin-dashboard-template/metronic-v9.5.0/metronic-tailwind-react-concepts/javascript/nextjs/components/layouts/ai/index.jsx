'use client';

import { ChatsProvider } from './components/chats-context';
import { LayoutProvider } from './components/context';
import { Wrapper } from './components/wrapper';

export function DefaultLayout({ children }) {
  return (
    <>
      <LayoutProvider
        bodyClassName="bg-muted"
        style={{
          '--sidebar-width': '255px',
          '--sidebar-header-height': '60px',
          '--header-height': '60px',
          '--header-height-mobile': '60px',
        }}
      >
        <ChatsProvider>
          <Wrapper>{children}</Wrapper>
        </ChatsProvider>
      </LayoutProvider>
    </>
  );
}
