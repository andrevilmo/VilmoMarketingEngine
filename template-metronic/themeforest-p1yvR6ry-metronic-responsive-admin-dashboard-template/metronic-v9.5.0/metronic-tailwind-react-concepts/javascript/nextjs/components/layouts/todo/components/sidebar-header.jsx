import Link from 'next/link';
import { PanelLeft, PanelRight } from 'lucide-react';
import { toAbsoluteUrl } from '@/lib/helpers';
import { Button } from '@/components/ui/button';
import { useLayout } from './context';

export function SidebarHeader() {
  const { sidebarToggle, isSidebarOpen } = useLayout();

  if (!isSidebarOpen) {
    return (
      <div className="flex items-center justify-center shrink-0 px-3 py-3.5">
        <Button
          mode="icon"
          variant="ghost"
          onClick={() => sidebarToggle()}
          className="hidden lg:inline-flex shrink-0"
        >
          <PanelRight />
        </Button>
      </div>
    );
  }

  return (
    <div className="flex items-center justify-between shrink-0 px-3 py-3.5">
      {/* Brand */}
      <Link href="/todo/all-tasks" className="flex items-center gap-2">
        <div
          className="
            flex items-center p-[6px]
            rounded-[60px] border border-[rgba(255,255,255,0.3)]
            bg-[#000]
            bg-[radial-gradient(97.49%_97.49%_at_50%_2.51%,rgba(255,255,255,0.5)_0%,rgba(255,255,255,0)_100%)]
            shadow-[0_0_0_1px_#000]
          "
        >
          <img
            src={toAbsoluteUrl('/media/app/logo-35.svg')}
            alt="image"
            className="min-w-[16px]"
          />
        </div>
        <span className="text-mono text-sm font-medium">KeenTodo</span>
      </Link>

      <Button
        mode="icon"
        variant="ghost"
        onClick={() => sidebarToggle()}
        className="hidden lg:inline-flex shrink-0"
      >
        <PanelLeft />
      </Button>
    </div>
  );
}
