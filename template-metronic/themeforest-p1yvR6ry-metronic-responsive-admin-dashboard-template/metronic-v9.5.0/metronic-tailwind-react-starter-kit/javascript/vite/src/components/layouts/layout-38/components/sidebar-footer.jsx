import { useLayout } from './context';
import { UserDropdownMenu } from './user-dropdown-menu';

export function SidebarFooter() {
  const { isSidebarOpen, isMobile } = useLayout();
  const isCollapsed = isMobile ? false : !isSidebarOpen;

  return (
    <div className="shrink-0 lg:px-2.5 py-2.5">
      <UserDropdownMenu isCollapsed={isCollapsed} />
    </div>
  );
}
