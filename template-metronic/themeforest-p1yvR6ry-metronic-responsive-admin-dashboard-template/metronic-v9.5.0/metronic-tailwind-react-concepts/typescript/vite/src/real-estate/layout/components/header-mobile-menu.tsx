import { Link, useLocation } from 'react-router-dom';
import {
  AccordionMenu,
  AccordionMenuGroup,
  AccordionMenuItem,
  AccordionMenuSub,
  AccordionMenuSubTrigger,
  AccordionMenuSubContent,
} from '@/components/ui/accordion-menu';
import type { MenuItem } from '../../types';
import { MENU_HEADER } from '../../mock';

export function HeaderMobileMenu() {
  const { pathname } = useLocation();

  const renderMenuItem = (item: MenuItem, index: number) => {
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
                <Link to={child.path} className="flex flex-col items-start">
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
        <Link to={item.path} className="flex items-center gap-2">
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
      <AccordionMenuGroup>
        {MENU_HEADER.map(renderMenuItem)}
      </AccordionMenuGroup>
    </AccordionMenu>
  );
}
