import { useLayout } from './context';
import { HeaderLogo } from './header-logo';
import { HeaderMenu } from './header-menu';
import { HeaderToolbar } from './header-toolbar';
import { Navbar } from './navbar';

export function Header() {
  const { isMobile } = useLayout();

  return (
    <header className="flex flex-col items-stretch bg-background fixed z-10 top-0 start-0 end-0 shrink-0 h-(--header-height-mobile) lg:h-(--header-height) lg:in-data-[header-sticky=true]:h-(--header-height-sticky) pe-[var(--removed-body-scroll-bar-size,0px)]">
      <div className="container-fluid grow flex items-stretch justify-between gap-2.5">
        <HeaderLogo />
        {!isMobile && <HeaderMenu />}
        <HeaderToolbar />
      </div>
      <Navbar />
    </header>
  );
}
