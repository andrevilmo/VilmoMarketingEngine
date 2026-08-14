import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { cn } from '@/lib/utils';
import {
  AccordionMenu,
  AccordionMenuIndicator,
  AccordionMenuItem,
  AccordionMenuSub,
  AccordionMenuSubContent,
  AccordionMenuSubTrigger,
} from '@/components/ui/accordion-menu';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { todoLists } from '@/app/todo/mock';

export function SidebarTodoList({ isCollapsed }) {
  const router = useRouter();
  const pathname = usePathname();

  // Determine active item based on current URL path
  const activeItem = todoLists.find((item) => item.path === pathname)?.id ?? 1;

  if (isCollapsed) {
    return (
      <div className="flex flex-col gap-1">
        {todoLists.map((todoList, index) => (
          <Tooltip key={index}>
            <TooltipTrigger asChild>
              <Button
                asChild
                variant="ghost"
                size="icon"
                className={cn(
                  'h-8.5 w-8.5',
                  activeItem === todoList.id
                    ? 'bg-muted text-foreground'
                    : 'text-muted-foreground hover:text-foreground hover:bg-muted',
                )}
              >
                <Link href={todoList.path}>
                  <todoList.icon
                    className={cn(
                      'size-4 transition-transform duration-200 hover:scale-110',
                      activeItem === todoList.id ? '' : 'text-muted-foreground',
                    )}
                  />
                </Link>
              </Button>
            </TooltipTrigger>
            <TooltipContent side="right">
              <div className="flex items-center gap-2">
                <span>{todoList.title}</span>
                <Badge
                  appearance="outline"
                  variant={
                    activeItem === todoList.id ? todoList.badge : todoList.badge
                  }
                  size="sm"
                >
                  {todoList.count}
                </Badge>
              </div>
            </TooltipContent>
          </Tooltip>
        ))}
      </div>
    );
  }

  return (
    <AccordionMenu
      type="single"
      collapsible
      defaultValue="todoLists-trigger"
      selectedValue="todoLists-trigger"
      className="space-y-7.5"
      classNames={{
        item: 'h-8.5 px-2.5 text-sm font-normal text-foreground data-[selected=true]:bg-muted data-[selected=true]:text-foreground [&[data-selected=true]_svg]:opacity-100 my-0.5',
        subTrigger:
          'text-xs font-normal text-muted-foreground hover:bg-transparent',
        subContent: 'ps-0',
      }}
    >
      <AccordionMenuSub value="todoLists">
        <AccordionMenuSubTrigger value="todoLists-trigger">
          <span>My Lists</span>
          <AccordionMenuIndicator />
        </AccordionMenuSubTrigger>
        <AccordionMenuSubContent
          type="single"
          collapsible
          parentValue="todoLists-trigger"
        >
          {todoLists.map((todoList, index) => (
            <AccordionMenuItem
              key={index}
              value={todoList.path}
              onClick={() => router.replace(todoList.path)}
              className={cn(
                'group flex w-full items-center gap-2',
                activeItem === todoList.id
                  ? 'bg-muted text-foreground'
                  : 'text-muted-foreground hover:text-foreground hover:bg-muted',
              )}
            >
              <todoList.icon
                className={cn(
                  'size-4 transition-transform duration-200 group-hover:scale-110',
                  activeItem === todoList.id ? '' : 'text-muted-foreground',
                )}
              />

              {todoList.title}
              <Badge
                appearance="outline"
                variant={todoList.badge}
                size="sm"
                className="ms-auto"
              >
                {todoList.count}
              </Badge>
            </AccordionMenuItem>
          ))}
        </AccordionMenuSubContent>
      </AccordionMenuSub>
    </AccordionMenu>
  );
}
