import { Helmet } from 'react-helmet-async';
import { Wrapper } from './components/wrapper';
import { LayoutProvider } from './components/context';
import { ChatsProvider } from './components/chats-context';

export function DefaultLayout() {

  return (
    <>
      <Helmet>
        <title>AI</title>
      </Helmet>

      <LayoutProvider
        bodyClassName="bg-muted"
        style={{
          '--sidebar-width': '255px',
          '--sidebar-header-height': '60px',
          '--header-height': '60px',
          '--header-height-mobile': '60px',
        } as React.CSSProperties}
      >
        <ChatsProvider>
          <Wrapper /> 
        </ChatsProvider>
      </LayoutProvider>
    </>
  );
}
