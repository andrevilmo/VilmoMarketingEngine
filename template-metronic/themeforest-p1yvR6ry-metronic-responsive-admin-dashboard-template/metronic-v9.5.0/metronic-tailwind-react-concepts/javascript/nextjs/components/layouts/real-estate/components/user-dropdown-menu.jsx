import {
  Bell,
  Heart,
  Home,
  LogOut,
  Moon,
  Settings,
  Shield,
  Sun,
  User,
} from 'lucide-react';
import { useTheme } from 'next-themes';
import { toAbsoluteUrl } from '@/lib/helpers';
import {
  Avatar,
  AvatarFallback,
  AvatarImage,
  AvatarIndicator,
  AvatarStatus,
} from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

export function UserDropdownMenu() {
  const { theme, setTheme } = useTheme();

  const toggleTheme = () => {
    setTheme(theme === 'light' ? 'dark' : 'light');
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="cursor-pointer">
        <Avatar className="size-7">
          <AvatarImage
            src={toAbsoluteUrl('/media/avatars/300-2.png')}
            alt="@reui"
          />
          <AvatarFallback>CH</AvatarFallback>
          <AvatarIndicator className="-end-2 -top-2">
            <AvatarStatus variant="online" className="size-2.5" />
          </AvatarIndicator>
        </Avatar>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        className="w-56"
        side="bottom"
        align="end"
        sideOffset={11}
      >
        {/* User Information Section */}
        <div className="flex items-center gap-3 px-3 py-2">
          <Avatar>
            <AvatarImage
              src={toAbsoluteUrl('/media/avatars/300-2.png')}
              alt="@reui"
            />
            <AvatarFallback>CH</AvatarFallback>
            <AvatarIndicator className="-end-1.5 -top-1.5">
              <AvatarStatus variant="online" className="size-2.5" />
            </AvatarIndicator>
          </Avatar>
          <div className="flex flex-col items-start">
            <span className="text-sm font-semibold text-foreground">
              Chris Harris
            </span>
            <span className="text-xs text-muted-foreground">
              chris.h@gmail.com
            </span>
          </div>
        </div>

        <DropdownMenuSeparator />

        {/* Core Actions */}
        <DropdownMenuItem>
          <Home />
          My Listings
          <Badge
            variant="info"
            size="sm"
            appearance="outline"
            className="ms-auto"
          >
            2
          </Badge>
        </DropdownMenuItem>

        <DropdownMenuItem>
          <Heart />
          Saved Properties
          <Badge
            variant="success"
            size="sm"
            appearance="outline"
            className="ms-auto"
          >
            4
          </Badge>
        </DropdownMenuItem>

        <DropdownMenuItem>
          <Bell />
          Search Alerts
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        {/* Theme Toggle */}
        <DropdownMenuItem onClick={toggleTheme}>
          {theme === 'light' ? (
            <Moon className="size-4" />
          ) : (
            <Sun className="size-4" />
          )}
          <span>{theme === 'light' ? 'Dark mode' : 'Light mode'}</span>
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        {/* Settings */}
        <DropdownMenuItem>
          <User />
          Profile Settings
        </DropdownMenuItem>

        <DropdownMenuItem>
          <Settings />
          Preferences
        </DropdownMenuItem>

        <DropdownMenuItem>
          <Shield />
          Security
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        {/* Action Items */}
        <DropdownMenuItem>
          <LogOut />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
