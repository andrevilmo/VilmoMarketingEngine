import { LayoutProvider } from './components/context';
import { Wrapper } from './components/wrapper';

// Generate metadata for the layout
export async function generateMetadata() {
  // You can access route params here if needed
  // const { params } = props;

  return {
    title: 'Dashboard',
    description: '',
  };
}

export function Layout39({ children }) {
  return (
    <LayoutProvider
      bodyClassName="bg-zinc-100 dark:bg-zinc-900 lg:overflow-hidden"
      style={{
        '--sidebar-width': '250px',
        '--sidebar-width-mobile': '225px',
        '--sidebar-width-collapsed': '60px',
        '--aside-width': '320px',
        '--aside-width-mobile': '300px',
        '--header-height': '60px',
        '--header-height-mobile': '70px',
      }}
    >
      <Wrapper>{children}</Wrapper>
    </LayoutProvider>
  );
}
