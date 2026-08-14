import Link from 'next/link';
import { Menu } from 'lucide-react';
import { useLayout } from './context';
import { useEffect, useState } from 'react';
import { toAbsoluteUrl } from '@/lib/helpers';
import { usePathname } from 'next/navigation';
import {
  Sheet,
  SheetBody,
  SheetContent,
  SheetHeader,
  SheetTrigger,
} from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { ScrollArea } from "@/components/ui/scroll-area";
import { HeaderMobileMenu } from './header-mobile-menu';

export function HeaderLogo() {
  const pathname = usePathname();
  const { isMobile } = useLayout();
  const [isSheetOpen, setIsSheetOpen] = useState(false);

  // Close sheet when route changes
  useEffect(() => {
    setIsSheetOpen(false);
  }, [pathname]);

  return (
    <div className="flex items-center gap-3">
      {/* Brand */}
      <Link href="/real-estate" className="flex items-center gap-2">
        <div
          className="
            flex items-center p-[5px]
            rounded-[6px] border border-white/30
            bg-[#000]
            bg-[radial-gradient(97.49%_97.49%_at_50%_2.51%,rgba(255,255,255,0.5)_0%,rgba(255,255,255,0)_100%)]
            shadow-[0_0_0_1px_#000]
          "
        >
          <img src={toAbsoluteUrl('/media/app/logo-35.svg')} alt="image" className="min-w-[18px]" />
        </div>
        <span className="text-mono text-lg font-semibold hidden lg:block">
          Metronic
        </span>
      </Link>

      {/* Mobile sidebar toggle */}
      {isMobile && (
        <Sheet open={isSheetOpen} onOpenChange={setIsSheetOpen}>
          <SheetTrigger asChild>
            <Button variant="ghost" mode="icon" size="sm" className="-ms-1.5">
              <Menu className="size-4" />
            </Button>
          </SheetTrigger>
          <SheetContent
            className="p-0 gap-0 w-[225px]"
            side="left"
            close={false}
          >
            <SheetHeader className="p-0 space-y-0" />
            <SheetBody className="flex flex-col grow p-0">
              <ScrollArea className="grow h-[calc(100vh-5.5rem)] lg:h-[calc(100vh-4rem)] mt-0">
                <HeaderMobileMenu />
              </ScrollArea>
            </SheetBody>
          </SheetContent>
        </Sheet>
      )}
    </div>
  );
}
