import Link from 'next/link';
import { ChevronDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { MENU_HEADER } from '@/app/real-estate/mock';

export function HeaderMenu() {
  const renderMenuItem = (item, index) => {
    const hasChildren = item.children && item.children.length > 0;

    if (hasChildren) {
      return (
        <DropdownMenu key={index}>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost">
              {item.icon && <item.icon />}
              {item.title}
              <ChevronDown />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start">
            {item.children?.map((child, childIndex) => (
              <DropdownMenuItem key={childIndex} asChild>
                <Link
                  href={child.path}
                  className="flex flex-col items-start cursor-pointer"
                >
                  <span className="text-sm font-semibold leading-none text-foreground">
                    {child.title}
                  </span>
                  {child.desc && (
                    <span className="text-xs font-normal text-muted-foreground">
                      {child.desc}
                    </span>
                  )}
                </Link>
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      );
    }

    return (
      <Button key={index} variant="ghost" asChild>
        <Link href={item.path}>
          {item.icon && <item.icon />}
          {item.title}
        </Link>
      </Button>
    );
  };

  return (
    <div className="flex items-stretch">
      <nav className="list-none flex items-center gap-2">
        {MENU_HEADER.map(renderMenuItem)}
      </nav>
    </div>
  );
}
