import { useState } from 'react';
import Link from 'next/link';
import { Menu, Sparkles } from 'lucide-react';
import { toAbsoluteUrl } from '@/lib/helpers';
import { Button } from '@/components/ui/button';
import {
  Sheet,
  SheetBody,
  SheetContent,
  SheetHeader,
  SheetTrigger,
} from '@/components/ui/sheet';
import { Aside } from './aside';
import { SidebarContent } from './sidebar-content';
import { SidebarFooter } from './sidebar-footer';
import { UserDropdownMenu } from './user-dropdown-menu';

export function HeaderMobile() {
  const [isSidebarSheetOpen, setIsSidebarSheetOpen] = useState(false);
  const [isAsideSheetOpen, setIsAsideSheetOpen] = useState(false);

  return (
    <header className="flex items-stretch fixed z-10 top-0 start-0 end-0 h-(--header-height-mobile) bg-zinc-100 dark:bg-zinc-900 pe-[var(--removed-body-scroll-bar-size,0px)]">
      <div className="flex items-stretch w-full bg-background border border-input rounded-xl shadow-xs m-2">
        <div className="container-fluid grow flex items-center justify-between gap-2.5">
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

          <div className="flex items-center gap-2">
            {/* Sidebar */}
            <Sheet
              open={isSidebarSheetOpen}
              onOpenChange={setIsSidebarSheetOpen}
            >
              <SheetTrigger asChild>
                <Button variant="ghost" mode="icon">
                  <Menu />
                </Button>
              </SheetTrigger>
              <SheetContent
                className="p-0 gap-0 w-(--sidebar-width-mobile)"
                side="left"
                close={false}
              >
                <SheetHeader className="p-0 space-y-0" />
                <SheetBody className="flex flex-col grow p-0">
                  <SidebarContent />
                  <SidebarFooter />
                </SheetBody>
              </SheetContent>
            </Sheet>

            {/* Aside */}
            <Sheet open={isAsideSheetOpen} onOpenChange={setIsAsideSheetOpen}>
              <SheetTrigger asChild>
                <Button variant="ghost" mode="icon">
                  <Sparkles className="size-4" />
                </Button>
              </SheetTrigger>
              <SheetContent
                className="p-0 gap-0 w-(--aside-width-mobile)"
                side="right"
                close={false}
              >
                <SheetHeader className="p-0 space-y-0" />
                <SheetBody className="flex flex-col grow p-0">
                  <Aside onClose={() => setIsAsideSheetOpen(false)} />
                </SheetBody>
              </SheetContent>
            </Sheet>

            <UserDropdownMenu isCollapsed={true} />
          </div>
        </div>
      </div>
    </header>
  );
}
