import { Helmet } from 'react-helmet-async';
import { Wrapper } from './components/wrapper';
import { LayoutProvider } from './components/context';

export function DefaultLayout() {

  return (
    <>
      <Helmet>
        <title>Real Estate</title>
      </Helmet>

      <LayoutProvider
        bodyClassName="lg:overflow-hidden"
        style={{
          '--header-height': '120px',
          '--navbar-height': '60px',
          '--header-height-sticky': '70px',
          '--header-height-mobile': '120px',
        } as React.CSSProperties}
      >
        <Wrapper />
      </LayoutProvider>
    </>
  );
}
