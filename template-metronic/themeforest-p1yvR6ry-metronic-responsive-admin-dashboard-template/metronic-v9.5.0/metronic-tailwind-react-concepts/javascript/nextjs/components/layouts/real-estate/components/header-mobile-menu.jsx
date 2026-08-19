import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  AccordionMenu,
  AccordionMenuGroup,
  AccordionMenuItem,
  AccordionMenuSub,
  AccordionMenuSubContent,
  AccordionMenuSubTrigger,
} from '@/components/ui/accordion-menu';
import { MENU_HEADER } from '@/app/real-estate/mock';

export function HeaderMobileMenu() {
  const pathname = usePathname();

  const renderMenuItem = (item, index) => {
    const hasChildren = item.children && item.children.length > 0;

    if (hasChildren) {
      return (
        <AccordionMenuSub key={index} value={item.title}>
          <AccordionMenuSubTrigger>
            {item.icon && <item.icon />}
            {item.title}
          </AccordionMenuSubTrigger>
          <AccordionMenuSubContent
            type="single"
            collapsible
            parentValue={item.title}
          >
            {item.children?.map((child, childIndex) => (
              <AccordionMenuItem key={childIndex} value={child.path} asChild>
                <Link href={child.path} className="flex flex-col items-start">
                  <span className="text-sm font-medium">{child.title}</span>
                  {child.desc && (
                    <span className="text-xs text-muted-foreground">
                      {child.desc}
                    </span>
                  )}
                </Link>
              </AccordionMenuItem>
            ))}
          </AccordionMenuSubContent>
        </AccordionMenuSub>
      );
    }

    return (
      <AccordionMenuItem key={index} value={item.path} asChild>
        <Link href={item.path} className="flex items-center gap-2">
          {item.icon && <item.icon />}
          {item.title}
        </Link>
      </AccordionMenuItem>
    );
  };

  return (
    <AccordionMenu
      type="single"
      collapsible
      className="p-2"
      matchPath={(path) => pathname === path || pathname.startsWith(path)}
    >
      <AccordionMenuGroup>{MENU_HEADER.map(renderMenuItem)}</AccordionMenuGroup>
    </AccordionMenu>
  );
}
