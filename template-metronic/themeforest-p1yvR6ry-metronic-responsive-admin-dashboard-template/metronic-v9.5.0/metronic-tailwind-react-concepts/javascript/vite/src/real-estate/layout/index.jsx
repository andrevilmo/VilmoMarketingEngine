import { Helmet } from 'react-helmet-async';
import { LayoutProvider } from './components/context';
import { Wrapper } from './components/wrapper';

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
        }}
      >
        <Wrapper />
      </LayoutProvider>
    </>
  );
}
