import { BellDot, Search, Settings } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useLayout } from './context';
import { UserDropdownMenu } from './user-dropdown-menu';

export function HeaderToolbar() {
  const { isMobile } = useLayout();

  return (
    <div className="flex items-center justify-end gap-2.5">
      {isMobile && (
        <Button
          variant="ghost"
          size="icon"
          className="text-muted-foreground hover:text-foreground"
        >
          <Search className="opacity-100" />
        </Button>
      )}
      <Button
        variant="ghost"
        size="icon"
        className="text-muted-foreground hover:text-foreground"
      >
        <BellDot className="opacity-100" />
      </Button>
      <Button
        variant="ghost"
        size="icon"
        className="text-muted-foreground hover:text-foreground"
      >
        <Settings className="opacity-100" />
      </Button>

      <UserDropdownMenu />
    </div>
  );
}
